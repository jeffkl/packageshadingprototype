using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace PackageShading.Tasks
{
    public sealed class ShadeAssembliesEx : Task
    {
        private static readonly char[] SplitChars = new[] { ';', ',' };

        public bool Debug { get; set; }

        public string[] FallbackTargetFrameworks { get; set; }

        [Required]
        public string IntermediateOutputPath { get; set; }

        [Required]
        public string NuGetPackageRoot { get; set; }

        [Required]
        public ITaskItem[] PackageReferences { get; set; }

        [Required]
        public string ProjectAssetsFile { get; set; }

        [Required]
        public ITaskItem[] References { get; set; }

        [Output]
        public ITaskItem[] ReferencesToAdd { get; set; }

        [Output]
        public ITaskItem[] ReferencesToRemove { get; set; }

        [Output]
        public ITaskItem[] ShadedAssemblies { get; set; }

        [Required]
        public string TargetFramework { get; set; }

        public override bool Execute()
        {
            if (Debug)
            {
                Debugger.Launch();
            }

            StrongNameKeyPair strongNameKeyPair = StrongNameKeyPair.Create();

            List<AssemblyToRename> assembliesToRename = GetAssembliesToShade(strongNameKeyPair);

            if (assembliesToRename == null)
            {
                return !Log.HasLoggedErrors;
            }

            ShadeAssemblies(assembliesToRename, strongNameKeyPair);

            List<ITaskItem> shadedAssemblies = new List<ITaskItem>(assembliesToRename.Count);
            List<ITaskItem> referencesToRemove = new List<ITaskItem>(assembliesToRename.Count);
            List<ITaskItem> referencesToAdd = new List<ITaskItem>(assembliesToRename.Count);

            foreach (AssemblyToRename assemblyToRename in assembliesToRename)
            {
                if (assemblyToRename.IsReference)
                {
                    TaskItem referenceToRemove = new TaskItem(assemblyToRename.FullPath);

                    referencesToRemove.Add(referenceToRemove);

                    TaskItem referenceToAdd = new TaskItem(assemblyToRename.ShadedPath, assemblyToRename.Metadata);

                    referenceToAdd.SetMetadata("HintPath", assemblyToRename.ShadedPath);
                    referenceToAdd.SetMetadata("OriginalPath", assemblyToRename.FullPath);

                    referencesToAdd.Add(referenceToAdd);
                }
                else
                {
                    TaskItem shadedAssembly = new TaskItem(Path.GetFullPath(assemblyToRename.ShadedPath), assemblyToRename.Metadata);

                    shadedAssembly.SetMetadata("IsShadedAssembly", bool.TrueString);

                    shadedAssemblies.Add(shadedAssembly);
                }
            }

            ShadedAssemblies = shadedAssemblies.ToArray();

            ReferencesToRemove = referencesToRemove.ToArray();

            ReferencesToAdd = referencesToAdd.ToArray();

            return !Log.HasLoggedErrors;
        }

        private List<AssemblyToRename> GetAssembliesToShade(StrongNameKeyPair strongNameKeyPair)
        {
            List<AssemblyToRename> assembliesToRename = new List<AssemblyToRename>();

            Dictionary<PackageIdentity, HashSet<PackageIdentity>> packages = LoadAssetsFile();

            if (packages == null)
            {
                return null;
            }

            foreach (ITaskItem packageReference in PackageReferences)
            {
                string shadeDependencies = packageReference.GetMetadata("ShadeDependencies");

                if (string.IsNullOrWhiteSpace(shadeDependencies))
                {
                    continue;
                }

                PackageIdentity packageIdentity = new PackageIdentity
                {
                    Id = packageReference.ItemSpec,
                    Version = packageReference.GetMetadata("Version")
                };

                if (packages.TryGetValue(packageIdentity, out HashSet<PackageIdentity> dependencies))
                {
                    foreach (string shadeDependency in shadeDependencies.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries))
                    {
                        PackageIdentity? packageToShade = dependencies.FirstOrDefault(i => string.Equals(i.Id, shadeDependency, StringComparison.OrdinalIgnoreCase));

                        if (packageToShade != null)
                        {
                            DirectoryInfo nearestCompatibleTargetFrameworkDirectoryInfo = GetNearest(packageToShade.Value);

                            if (nearestCompatibleTargetFrameworkDirectoryInfo == null)
                            {
                                continue;
                            }

                            foreach (FileInfo assemblyFileInfo in nearestCompatibleTargetFrameworkDirectoryInfo.EnumerateFiles("*.dll"))
                            {
                                AssemblyToRename assemblyToRename = new AssemblyToRename(assemblyFileInfo.FullName);

                                string assemblyName = $"{assemblyToRename.AssemblyName.Name}.{assemblyToRename.AssemblyName.Version}";

                                assemblyToRename.ShadedAssemblyName = new AssemblyNameDefinition(assemblyName, assemblyToRename.AssemblyName.Version)
                                {
                                    Culture = assemblyToRename.AssemblyName.CultureName,
                                    PublicKey = strongNameKeyPair.PublicKey,
                                };

                                // The location for the shaded assembly is under "obj"
                                assemblyToRename.ShadedPath = Path.GetFullPath(Path.Combine(IntermediateOutputPath, "ShadedAssemblies", $"{assemblyName}.dll"));

                                assemblyToRename.Metadata["NuGetPackageId"] = packageToShade.Value.Id;
                                assemblyToRename.Metadata["NuGetPackageVersion"] = packageToShade.Value.Version;

                                Log.LogMessageFromText($"Shading assembly reference {assemblyToRename.FullPath} => {assemblyToRename.ShadedPath}", MessageImportance.High);

                                assembliesToRename.Add(assemblyToRename);
                            }
                        }
                    }
                }
            }

            if (!assembliesToRename.Any())
            {
                return null;
            }

            Dictionary<string, List<(string AssemblyFullPath, AssemblyName AssemblyName)>> assemblyReferencesByAssemblyName = new Dictionary<string, List<(string AssemblyFullPath, AssemblyName AssemblyName)>>(StringComparer.OrdinalIgnoreCase);

            using DefaultAssemblyResolver resolver = new DefaultAssemblyResolver();

            foreach (ITaskItem item in References)
            {
                FileInfo assemblyFileInfo = new FileInfo(item.GetMetadata("FullPath"));

                resolver.AddSearchDirectory(assemblyFileInfo.DirectoryName);

                using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(assemblyFileInfo.FullName, new ReaderParameters
                {
                    AssemblyResolver = resolver,
                    ReadSymbols = File.Exists(Path.ChangeExtension(assemblyFileInfo.FullName, ".pdb"))
                });

                foreach (AssemblyNameReference assemblyReference in assembly.MainModule.AssemblyReferences)
                {
                    if (!assemblyReferencesByAssemblyName.TryGetValue(assemblyReference.FullName, out List<(string AssemblyFullPath, AssemblyName AssemblyName)> assemblyReferences))
                    {
                        assemblyReferences = new List<(string AssemblyFullPath, AssemblyName AssemblyName)>();

                        assemblyReferencesByAssemblyName.Add(assemblyReference.FullName, assemblyReferences);
                    }

                    assemblyReferences.Add((assemblyFileInfo.FullName, new AssemblyName(assembly.Name.FullName)));
                }
            }

            Stack<AssemblyToRename> stack = new Stack<AssemblyToRename>(assembliesToRename);

            while (stack.Any())
            {
                AssemblyToRename value = stack.Pop();

                foreach ((string assemblyFullPath, AssemblyName assemblyName) in assemblyReferencesByAssemblyName[value.AssemblyName.FullName])
                {
                    if (!assemblyReferencesByAssemblyName.ContainsKey(assemblyName.FullName))
                    {
                        continue;
                    }

                    ITaskItem originalReferenceItem = References.FirstOrDefault(i => string.Equals(i.GetMetadata("FullPath"), assemblyFullPath, StringComparison.OrdinalIgnoreCase));

                    AssemblyToRename assemblyToRename = new AssemblyToRename(assemblyFullPath, originalReferenceItem)
                    {
                        IsReference = true,
                        ShadedAssemblyName = new AssemblyNameDefinition(assemblyName.Name, assemblyName.Version)
                        {
                            Culture = assemblyName.CultureName,
                            PublicKey = strongNameKeyPair.PublicKey,
                        },
                        ShadedPath = Path.GetFullPath(Path.Combine(IntermediateOutputPath, "ShadedAssemblies", Path.GetFileName(assemblyFullPath)))
                    };

                    assembliesToRename.Add(assemblyToRename);

                    stack.Push(assemblyToRename);
                }
            }

            return assembliesToRename;
        }

        private DirectoryInfo GetNearest(PackageIdentity packageIdentity)
        {
            string path = Path.Combine(NuGetPackageRoot, packageIdentity.Id.ToLower(), packageIdentity.Version.ToLower(), "lib");

            List<DirectoryInfo> compatibleTargetFrameworks = Directory.EnumerateDirectories(path).Select(i => new DirectoryInfo(i)).ToList();

            DirectoryInfo result = NuGetFrameworkUtility.GetNearest(
                compatibleTargetFrameworks,
                NuGetFramework.ParseFolder(TargetFramework),
                (directoryInfo) => NuGetFramework.ParseFolder(directoryInfo.Name));

            if (result != null)
            {
                return result;
            }

            if (result == null && FallbackTargetFrameworks != null && FallbackTargetFrameworks.Any())
            {
                foreach (string fallbackTargetFramework in FallbackTargetFrameworks)
                {
                    result = NuGetFrameworkUtility.GetNearest(
                        compatibleTargetFrameworks,
                        NuGetFramework.ParseFolder(fallbackTargetFramework),
                        (directoryInfo) => NuGetFramework.ParseFolder(directoryInfo.Name));

                    if (result != null)
                    {
                        return result;
                    }
                }
            }

            return null;
        }

        private Dictionary<PackageIdentity, HashSet<PackageIdentity>> LoadAssetsFile()
        {
            Dictionary<PackageIdentity, HashSet<PackageIdentity>> packages = new Dictionary<PackageIdentity, HashSet<PackageIdentity>>();

            JsonDocumentOptions options = new JsonDocumentOptions
            {
                AllowTrailingCommas = true
            };

            using (FileStream stream = File.OpenRead(ProjectAssetsFile))
            {
                using (JsonDocument json = JsonDocument.Parse(stream, options))
                {
                    JsonProperty? target = json.RootElement.GetProperty("targets").EnumerateObject().FirstOrDefault(i => i.NameEquals(TargetFramework));

                    if (target == null || target.Value.Value.ValueKind == JsonValueKind.Undefined)
                    {
                        return null;
                    }

                    foreach (JsonProperty item in target.Value.Value.EnumerateObject())
                    {
                        string[] packageDetails = item.Name.Split('/');

                        PackageIdentity packageIdentity = new PackageIdentity
                        {
                            Id = packageDetails[0],
                            Version = packageDetails[1]
                        };

                        if (!packages.TryGetValue(packageIdentity, out HashSet<PackageIdentity> dependencies))
                        {
                            dependencies = new HashSet<PackageIdentity>();

                            packages[packageIdentity] = dependencies;
                        }

                        if (item.Value.TryGetProperty("dependencies", out JsonElement deps))
                        {
                            foreach (JsonProperty p in deps.EnumerateObject())
                            {
                                dependencies.Add(new PackageIdentity
                                {
                                    Id = p.Name,
                                    Version = p.Value.GetString()
                                });
                            }
                        }
                    }
                }
            }

            bool added = false;
            int count = 0;
            do
            {
                count++;
                added = false;

                foreach (KeyValuePair<PackageIdentity, HashSet<PackageIdentity>> package in packages)
                {
                    List<PackageIdentity> dependenciesToAdd = new List<PackageIdentity>();

                    foreach (PackageIdentity dependency in package.Value)
                    {
                        if (packages.TryGetValue(dependency, out HashSet<PackageIdentity> dependencies))
                        {
                            dependenciesToAdd.AddRange(dependencies);
                        }
                    }

                    foreach (PackageIdentity dependencyToAdd in dependenciesToAdd)
                    {
                        if (package.Value.Add(dependencyToAdd))
                        {
                            added = true;
                        }
                    }
                }
            }
            while (added);

            return packages;
        }

        private void ShadeAssemblies(List<AssemblyToRename> assembliesToShade, StrongNameKeyPair strongNameKeyPair)
        {
            foreach (AssemblyToRename assemblyToRename in assembliesToShade)
            {
                using (AssemblyDefinition assembly = assemblyToRename.ReadAssembly())
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(assemblyToRename.ShadedPath));

                    // This renames the assembly "name", not the file name
                    assembly.Name.Name = assemblyToRename.ShadedAssemblyName.Name;

                    // Remove the strong name signature flag, this is updated if we add a strong name later
                    assembly.MainModule.Attributes &= ~ModuleAttributes.StrongNameSigned;

                    // Loop through the assembly references to find any that need to be updated
                    foreach (AssemblyNameReference assemblyReference in assembly.MainModule.AssemblyReferences)
                    {
                        // Find a renamed assembly that matches the reference
                        AssemblyToRename newReference = assembliesToShade.FirstOrDefault(i => assemblyReference.FullName == i.AssemblyName.FullName);

                        if (newReference != null)
                        {
                            Log.LogMessageFromText($"  - {assemblyReference.FullName} -> {newReference.ShadedAssemblyName.FullName}", MessageImportance.Normal);

                            // Update the reference name and public key token
                            assemblyReference.Name = newReference.ShadedAssemblyName.Name;
                            assemblyReference.PublicKeyToken = strongNameKeyPair.PublicKeyToken;
                        }
                    }

                    assembly.Write(
                        assemblyToRename.ShadedPath,
                        new WriterParameters
                        {
                            StrongNameKeyPair = strongNameKeyPair,
                        });
                }

                // This just verifies that the assembly isn't broken.  If it is, ResolveAssemblyReferences fails later on, I just put
                // this here so I could debug it
                AssemblyName _ = AssemblyName.GetAssemblyName(assemblyToRename.ShadedPath);
            }
        }

        [DebuggerDisplay("{Id,nq}/{Version,nq}")]
        internal struct PackageIdentity : IEqualityComparer<PackageIdentity>, IEquatable<PackageIdentity>
        {
            public string Id { get; set; }

            public string Version { get; set; }

            public bool Equals(PackageIdentity x, PackageIdentity y) => string.Equals(x.Id, y.Id, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Version, y.Version, StringComparison.OrdinalIgnoreCase);

            public bool Equals(PackageIdentity other) => string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase) && string.Equals(Version, other.Version, StringComparison.OrdinalIgnoreCase);

            public int GetHashCode(PackageIdentity obj) => HashCode.Combine(obj.Id, obj.Version);
        }
    }
}