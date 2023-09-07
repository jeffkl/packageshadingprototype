using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PackageShading.Tasks
{
    /// <summary>
    /// Represents an MSBuild task that gets the assemblies to shade.
    /// </summary>
    public sealed class GetAssembliesToShade : Task
    {
        private static readonly ConcurrentDictionary<string, Lazy<NuGetAssetsFile>> AssetsFileCache = new ConcurrentDictionary<string, Lazy<NuGetAssetsFile>>(StringComparer.OrdinalIgnoreCase);
        private static readonly char[] SplitChars = new[] { ';', ',' };
        private readonly IAssemblyInformationReader _assemblyInformationReader;
        private readonly INuGetAssetFileLoader _nuGetAssetFileLoader;
        private readonly IPackageAssemblyResolver _packageAssemblyResolver;

        public GetAssembliesToShade()
            : this(new NuGetAssetFileLoader(), new PackageAssemblyResolver(), new AssemblyInformationReader())
        {
        }

        public GetAssembliesToShade(INuGetAssetFileLoader nuGetAssetFileLoader, IPackageAssemblyResolver packageAssemblyResolver, IAssemblyInformationReader assemblyInformationReader)
        {
            _nuGetAssetFileLoader = nuGetAssetFileLoader ?? throw new ArgumentNullException(nameof(nuGetAssetFileLoader));
            _packageAssemblyResolver = packageAssemblyResolver ?? throw new ArgumentNullException(nameof(packageAssemblyResolver));
            _assemblyInformationReader = assemblyInformationReader ?? throw new ArgumentNullException(nameof(assemblyInformationReader));
        }

        /// <summary>
        /// Gets or sets an array of <see cref="ITaskItem" /> representing the assemblies to shade.
        /// </summary>
        [Output]
        public ITaskItem[] AssembliesToShade { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to launch the debugger when the task is executed.
        /// </summary>
        public bool Debug { get; set; }

        /// <summary>
        /// Gets or sets an array of fallback target frameworks.
        /// </summary>
        public string[] FallbackTargetFrameworks { get; set; }

        /// <summary>
        /// Gets or sets the intermediate output path of the project.
        /// </summary>
        [Required]
        public string IntermediateOutputPath { get; set; }

        /// <summary>
        /// Gets or sets the root path to the global NuGet package folder.
        /// </summary>
        [Required]
        public string NuGetPackageRoot { get; set; }

        /// <summary>
        /// Gets or sets an array of <see cref="ITaskItem" /> objects representing the NuGet package references.
        /// </summary>
        [Required]
        public ITaskItem[] PackageReferences { get; set; }

        /// <summary>
        /// Gets or sets an array of <see cref="ITaskItem" /> objects representing the NuGet package versions.
        /// </summary>
        public ITaskItem[] PackageVersions { get; set; }

        /// <summary>
        /// Gets or sets the full path to the NuGet assets file.
        /// </summary>
        [Required]
        public string ProjectAssetsFile { get; set; }

        /// <summary>
        /// Gets or sets the full path to the project directory.
        /// </summary>
        public string ProjectDirectory { get; set; }

        /// <summary>
        /// Gets or sets an array of <see cref="ITaskItem" /> objects representing the project references.
        /// </summary>
        public ITaskItem[] ProjectReferences { get; set; }

        /// <summary>
        /// Gets or sets an array of <see cref="ITaskItem" /> objects representing the project references to add.
        /// </summary>
        [Output]
        public ITaskItem[] ProjectReferencesToAdd { get; set; }

        /// <summary>
        /// Gets or sets an array of <see cref="ITaskItem" /> objects representing the project references to remove.
        /// </summary>
        [Output]
        public ITaskItem[] ProjectReferencesToRemove { get; set; }

        /// <summary>
        /// Gets or sets an array of <see cref="ITaskItem" /> objects representing the reference assemblies.
        /// </summary>
        [Required]
        public ITaskItem[] References { get; set; }

        /// <summary>
        /// Gets or sets an array of <see cref="ITaskItem" /> objects representing the references to add.
        /// </summary>
        [Output]
        public ITaskItem[] ReferencesToAdd { get; set; }

        /// <summary>
        /// Gets or sets an array of <see cref="ITaskItem" /> objects representing the references to remove.
        /// </summary>
        [Output]
        public ITaskItem[] ReferencesToRemove { get; set; }

        /// <summary>
        /// Gets or sets the full path to the key file used to strong name sign the shaded assembly.
        /// </summary>
        [Required]
        public string ShadedAssemblyKeyFile { get; set; }

        /// <summary>
        /// Gets or sets the target framework of the current project.
        /// </summary>
        [Required]
        public string TargetFramework { get; set; }

        /// <summary>
        /// Gets or sets the target framework moniker of the current project.
        /// </summary>
        [Required]
        public string TargetFrameworkMoniker { get; set; }

        public override bool Execute()
        {
            if (Debug)
            {
                Debugger.Launch();
            }

            StrongNameKeyPair strongNameKeyPair = new StrongNameKeyPair(ShadedAssemblyKeyFile);

            Lazy<NuGetAssetsFile> assetsFileLazy = AssetsFileCache.GetOrAdd(ProjectAssetsFile, assetsFile => new Lazy<NuGetAssetsFile>(() => _nuGetAssetFileLoader.LoadAssetsFile(ProjectDirectory, assetsFile)));

            NuGetAssetsFile packagesByTargetFramework = assetsFileLazy.Value;

            if (packagesByTargetFramework == null)
            {
                return !Log.HasLoggedErrors;
            }

            if (!packagesByTargetFramework.TryGetValue(TargetFramework, out NuGetAssetsFileSection section) && !packagesByTargetFramework.TryGetValue(TargetFrameworkMoniker, out section))
            {
                return !Log.HasLoggedErrors;
            }

            HashSet<PackageIdentity> packagesToShade = GetPackagesToShade(assetsFileLazy.Value, section);

            if (!packagesToShade.Any())
            {
                return !Log.HasLoggedErrors;
            }

            List<AssemblyToRename> assembliesToRename = GetAssembliesToShadeForPackages(strongNameKeyPair, packagesToShade, assetsFileLazy.Value);

            if (!assembliesToRename.Any())
            {
                return !Log.HasLoggedErrors;
            }

            AddAssembliesWithReferencesToUpdate(strongNameKeyPair, assembliesToRename);

            SetOutputParameters(assembliesToRename);

            return !Log.HasLoggedErrors;
        }

        private void AddAssembliesWithReferencesToUpdate(StrongNameKeyPair strongNameKeyPair, List<AssemblyToRename> assembliesToRename)
        {
            AssemblyInformation assemblyInformation = _assemblyInformationReader.GetAssemblyInformation(References.Select(i => i.GetMetadata(ItemMetadataNames.FullPath)));

            Stack<AssemblyToRename> stack = new Stack<AssemblyToRename>(assembliesToRename);

            while (stack.Any())
            {
                AssemblyToRename value = stack.Pop();

                if (!assemblyInformation.AssemblyReferences.TryGetValue(value.AssemblyName.FullName, out List<AssemblyReference> assemblyReferences))
                {
                    continue;
                }

                foreach (AssemblyReference assemblyReference in assemblyReferences)
                {
                    if (!assemblyInformation.AssemblyReferences.ContainsKey(assemblyReference.Name.FullName))
                    {
                        continue;
                    }

                    if (assembliesToRename.Any(i => string.Equals(i.AssemblyName.FullName, assemblyReference.Name.FullName)))
                    {
                        continue;
                    }

                    using AssemblyDefinition assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyReference.FullPath);

                    AssemblyToRename assemblyToRename = new AssemblyToRename(assemblyReference.FullPath, assemblyReference.Name)
                    {
                        ShadedAssemblyName = new AssemblyNameDefinition(assemblyReference.Name.Name, assemblyReference.Name.Version)
                        {
                            Culture = assemblyReference.Name.CultureName,
                            PublicKey = strongNameKeyPair.PublicKey,
                        },
                        ShadedPath = Path.GetFullPath(Path.Combine(IntermediateOutputPath, "ShadedAssemblies", Path.GetFileName(assemblyReference.FullPath)))
                    };

                    assemblyToRename.InternalsVisibleTo = InternalsVisibleToCache.GetInternalsVisibleTo(assemblyReference.FullPath);
                    assemblyToRename.ShadedInternalsVisibleTo = $"{assemblyToRename.ShadedAssemblyName.Name}, PublicKey={strongNameKeyPair.PublicKeyString}";

                    assembliesToRename.Add(assemblyToRename);

                    stack.Push(assemblyToRename);

                    assembliesToRename.AddRange(GetResourceAssemblies(strongNameKeyPair, assemblyToRename));
                }
            }

            List<AssemblyToRename> assembliesWithInternalsVisibleTo = new List<AssemblyToRename>();

            HashSet<string> assembliesToUpdate = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (AssemblyToRename existingAssemblyToRename in assembliesToRename)
            {
                if (existingAssemblyToRename.InternalsVisibleTo != null && assemblyInformation.FriendAssemblies.TryGetValue(existingAssemblyToRename.InternalsVisibleTo, out List<AssemblyReference> friendAssemblies))
                {
                    foreach (AssemblyReference assemblyReference in friendAssemblies)
                    {
                        if (assembliesToRename.Any(i => i.AssemblyName.FullName.Equals(assemblyReference.Name.FullName)))
                        {
                            continue;
                        }

                        var assemblyName = AssemblyNameCache.GetAssemblyName(assemblyReference.FullPath);

                        if (!assembliesToUpdate.Add(assemblyName.FullName))
                        {
                            continue;
                        }

                        AssemblyToRename assemblyToRename = new AssemblyToRename(assemblyReference.FullPath, assemblyName)
                        {
                            ShadedAssemblyName = new AssemblyNameDefinition(assemblyName.Name, assemblyName.Version)
                            {
                                Culture = assemblyName.CultureName,
                                PublicKey = strongNameKeyPair.PublicKey,
                            },
                            ShadedPath = Path.GetFullPath(Path.Combine(IntermediateOutputPath, "ShadedAssemblies", Path.GetFileName(assemblyReference.FullPath)))
                        };

                        assemblyToRename.InternalsVisibleTo = InternalsVisibleToCache.GetInternalsVisibleTo(assemblyReference.FullPath);
                        assemblyToRename.ShadedInternalsVisibleTo = $"{assemblyToRename.ShadedAssemblyName.Name}, PublicKey={strongNameKeyPair.PublicKeyString}";

                        assembliesWithInternalsVisibleTo.Add(assemblyToRename);
                    }
                }
            }

            foreach (var item in assembliesWithInternalsVisibleTo)
            {
                assembliesToRename.Add(item);
            }
        }

        private List<AssemblyToRename> GetAssembliesToShadeForPackages(StrongNameKeyPair strongNameKeyPair, HashSet<PackageIdentity> packagesToShade, NuGetAssetsFile assetsFile)
        {
            HashSet<string> assemblyNames = new HashSet<string>();

            List<AssemblyToRename> assembliesToRename = new List<AssemblyToRename>();

            foreach (PackageIdentity packageToShade in packagesToShade)
            {
                if (packageToShade.Id != null)
                {
                    IEnumerable<(string Path, string Subdirectory, AssemblyName Name)> assemblyFiles = _packageAssemblyResolver.GetNearest(packageToShade, NuGetPackageRoot, TargetFramework, FallbackTargetFrameworks);

                    if (assemblyFiles == null)
                    {
                        KeyValuePair<string, PackageIdentity> projectReference = assetsFile.ProjectReferences.FirstOrDefault(i => i.Value.Equals(packageToShade));

                        if (projectReference.Key is null)
                        {
                            continue;
                        }
                        ITaskItem projectReferenceItem = References.FirstOrDefault(i => string.Equals(i.GetMetadata("MSBuildSourceProjectFile"), projectReference.Key));

                        if (projectReferenceItem == null)
                        {
                            continue;
                        }

                        string assemblyPath = projectReferenceItem.GetMetadata("FullPath");

                        assemblyFiles = new List<(string Path, string Subdirectory, AssemblyName Name)>(capacity: 1)
                            {
                                (Path: assemblyPath, Subdirectory: string.Empty, Name: AssemblyNameCache.GetAssemblyName(assemblyPath))
                            };
                    }

                    foreach ((string Path, string Subdirectory, AssemblyName Name) assemblyFile in assemblyFiles)
                    {
                        if (!assemblyNames.Add(assemblyFile.Name.FullName))
                        {
                            continue;
                        }

                        AssemblyToRename assemblyToRename = new AssemblyToRename(assemblyFile.Path, assemblyFile.Name)
                        {
                            DestinationSubdirectory = string.IsNullOrWhiteSpace(assemblyFile.Subdirectory) ? string.Empty : assemblyFile.Subdirectory + Path.DirectorySeparatorChar,
                        };

                        string assemblyName = $"{assemblyToRename.AssemblyName.Name}.{assemblyToRename.AssemblyName.Version}";

                        assemblyToRename.ShadedAssemblyName = new AssemblyNameDefinition(assemblyName, assemblyToRename.AssemblyName.Version)
                        {
                            Culture = assemblyToRename.AssemblyName.CultureName,
                            PublicKey = strongNameKeyPair.PublicKey,
                        };

                        assemblyToRename.InternalsVisibleTo = InternalsVisibleToCache.GetInternalsVisibleTo(assemblyFile.Path);
                        assemblyToRename.ShadedInternalsVisibleTo = $"{assemblyToRename.ShadedAssemblyName.Name}, PublicKey={strongNameKeyPair.PublicKeyString}";
                        assemblyToRename.ShadedPath = Path.GetFullPath(Path.Combine(IntermediateOutputPath, "ShadedAssemblies", assemblyFile.Subdirectory ?? string.Empty, $"{assemblyName}.dll"));

                        assembliesToRename.Add(assemblyToRename);
                    }
                }
            }

            return assembliesToRename;
        }

        private Dictionary<string, List<ITaskItem>> GetItemsByAssemblyName(IEnumerable<ITaskItem> items)
        {
            Dictionary<string, List<ITaskItem>> existingReferenceItems = new Dictionary<string, List<ITaskItem>>(StringComparer.OrdinalIgnoreCase);

            foreach (ITaskItem referenceItem in items)
            {
                if (!File.Exists(referenceItem.ItemSpec))
                {
                    continue;
                }

                AssemblyName assemblyName = AssemblyNameCache.GetAssemblyName(referenceItem.ItemSpec);

                if (!existingReferenceItems.TryGetValue(assemblyName.FullName, out List<ITaskItem> references))
                {
                    references = new List<ITaskItem>();

                    existingReferenceItems.Add(assemblyName.FullName, references);
                }

                references.Add(referenceItem);
            }

            return existingReferenceItems;
        }

        private HashSet<PackageIdentity> GetPackagesToShade(NuGetAssetsFile assetsFile, NuGetAssetsFileSection assetsFileSection)
        {
            HashSet<PackageIdentity> packagesToShade = new HashSet<PackageIdentity>();

            Dictionary<string, string> packageVersions = PackageVersions != null && PackageVersions.Any() ? PackageVersions.ToDictionary(i => i.ItemSpec, i => i.GetMetadata(ItemMetadataNames.Version), StringComparer.OrdinalIgnoreCase) : null;

            foreach (ITaskItem packageReference in PackageReferences)
            {
                string shadeDependencies = packageReference.GetMetadata(ItemMetadataNames.ShadeDependencies);
                string shade = packageReference.GetMetadata(ItemMetadataNames.Shade);

                if (string.IsNullOrWhiteSpace(shade) && string.IsNullOrWhiteSpace(shadeDependencies))
                {
                    continue;
                }

                PackageIdentity packageIdentity = new PackageIdentity(packageReference.ItemSpec, GetPackageVersion(packageReference, packageVersions));

                if (string.Equals(shade, bool.TrueString, StringComparison.OrdinalIgnoreCase))
                {
                    packagesToShade.Add(packageIdentity);
                }

                if (!string.IsNullOrWhiteSpace(shadeDependencies))
                {
                    if (assetsFileSection.Packages.TryGetValue(packageIdentity, out HashSet<PackageIdentity> dependencies))
                    {
                        foreach (string shadeDependency in shadeDependencies.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries))
                        {
                            PackageIdentity packageToShade = dependencies.FirstOrDefault(i => string.Equals(i.Id, shadeDependency, StringComparison.OrdinalIgnoreCase));

                            packagesToShade.Add(packageToShade);
                        }
                    }
                }
            }

            foreach (ITaskItem packageReference in ProjectReferences)
            {
                string shadeDependencies = packageReference.GetMetadata(ItemMetadataNames.ShadeDependencies);
                string shade = packageReference.GetMetadata(ItemMetadataNames.Shade);

                if (string.IsNullOrWhiteSpace(shade) && string.IsNullOrWhiteSpace(shadeDependencies))
                {
                    continue;
                }

                string fullPath = packageReference.GetMetadata("FullPath");

                if (!assetsFile.ProjectReferences.TryGetValue(fullPath, out PackageIdentity packageIdentity))
                {
                    continue;
                }

                if (string.Equals(shade, bool.TrueString, StringComparison.OrdinalIgnoreCase))
                {
                    packagesToShade.Add(packageIdentity);
                }

                if (!string.IsNullOrWhiteSpace(shadeDependencies))
                {
                    if (assetsFileSection.Packages.TryGetValue(packageIdentity, out HashSet<PackageIdentity> dependencies))
                    {
                        if (shadeDependencies == "*")
                        {
                            foreach (var item in dependencies)
                            {
                                packagesToShade.Add(item);
                            }
                        }
                        else
                        {
                            foreach (string shadeDependency in shadeDependencies.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries))
                            {
                                PackageIdentity packageToShade = dependencies.FirstOrDefault(i => string.Equals(i.Id, shadeDependency, StringComparison.OrdinalIgnoreCase));

                                packagesToShade.Add(packageToShade);
                            }
                        }
                    }
                }
            }

            return packagesToShade;

            string GetPackageVersion(ITaskItem packageReference, Dictionary<string, string> packageVersions)
            {
                string packageVersion = packageReference.GetMetadata(ItemMetadataNames.Version);

                if (!string.IsNullOrEmpty(packageVersion))
                {
                    return packageVersion;
                }

                packageVersion = packageReference.GetMetadata(ItemMetadataNames.VersionOverride);

                if (!string.IsNullOrEmpty(packageVersion))
                {
                    return packageVersion;
                }

                if (packageVersions != null && packageVersions.TryGetValue(packageReference.ItemSpec, out packageVersion))
                {
                    return packageVersion;
                }

                return string.Empty;
            }
        }

        private IEnumerable<AssemblyToRename> GetResourceAssemblies(StrongNameKeyPair strongNameKeyPair, AssemblyToRename assemblyToRename)
        {
            FileInfo assemblyFile = new FileInfo(assemblyToRename.FullPath);

            if (!assemblyFile.Exists)
            {
                yield break;
            }

            foreach (FileInfo resourceAssemblyFile in Directory.EnumerateFiles(assemblyFile.DirectoryName, Path.ChangeExtension(assemblyFile.Name, ".resources.dll"), SearchOption.AllDirectories).Select(i => new FileInfo(i)))
            {
                string subdirectory = string.Equals(resourceAssemblyFile.DirectoryName, assemblyFile.DirectoryName, StringComparison.OrdinalIgnoreCase)
                    ? string.Empty
                    : resourceAssemblyFile.DirectoryName.Substring(assemblyFile.DirectoryName.Length + 1) + Path.DirectorySeparatorChar;

                AssemblyName resourceAssemblyName = AssemblyNameCache.GetAssemblyName(resourceAssemblyFile.FullName);

                yield return new AssemblyToRename(resourceAssemblyFile.FullName, resourceAssemblyName)
                {
                    ShadedAssemblyName = new AssemblyNameDefinition(resourceAssemblyName.Name, resourceAssemblyName.Version)
                    {
                        Culture = resourceAssemblyName.CultureName,
                        PublicKey = strongNameKeyPair.PublicKey,
                    },
                    ShadedPath = Path.GetFullPath(Path.Combine(IntermediateOutputPath, "ShadedAssemblies", subdirectory, resourceAssemblyFile.Name)),
                    DestinationSubdirectory = subdirectory,
                };
            }
        }

        private void SetOutputParameters(IReadOnlyCollection<AssemblyToRename> assembliesToRename)
        {
            List<ITaskItem> assembliesToShade = new List<ITaskItem>(assembliesToRename.Count);
            List<ITaskItem> referencesToRemove = new List<ITaskItem>(assembliesToRename.Count);
            List<ITaskItem> referencesToAdd = new List<ITaskItem>(assembliesToRename.Count);
            List<ITaskItem> projectReferencesToAdd = new List<ITaskItem>(assembliesToRename.Count);
            List<ITaskItem> projectReferencesToRemove = new List<ITaskItem>(assembliesToRename.Count);

            Dictionary<string, List<ITaskItem>> existingReferenceItems = GetItemsByAssemblyName(References);

            foreach (AssemblyToRename assemblyToRename in assembliesToRename)
            {
                TaskItem assemblyToShade = new TaskItem(Path.GetFullPath(assemblyToRename.ShadedPath), assemblyToRename.Metadata);

                assemblyToShade.SetMetadata(ItemMetadataNames.OriginalPath, assemblyToRename.FullPath);
                assemblyToShade.SetMetadata(ItemMetadataNames.ShadedAssemblyName, assemblyToRename.ShadedAssemblyName.FullName);
                assemblyToShade.SetMetadata(ItemMetadataNames.AssemblyName, assemblyToRename.AssemblyName.FullName);
                assemblyToShade.SetMetadata(ItemMetadataNames.InternalsVisibleTo, assemblyToRename.InternalsVisibleTo);
                assemblyToShade.SetMetadata(ItemMetadataNames.ShadedInternalsVisibleTo, assemblyToRename.ShadedInternalsVisibleTo);

                assemblyToShade.SetMetadata(ItemMetadataNames.DestinationSubdirectory, string.IsNullOrWhiteSpace(assemblyToRename.DestinationSubdirectory) ? string.Empty : assemblyToRename.DestinationSubdirectory);

                if (existingReferenceItems.TryGetValue(assemblyToRename.AssemblyName.FullName, out List<ITaskItem> existingReferenceItemsForAssembly))
                {
                    bool isProjectReference = false;

                    foreach (ITaskItem existingReferenceItemForAssembly in existingReferenceItemsForAssembly)
                    {
                        if (string.Equals(existingReferenceItemForAssembly.GetMetadata("ReferenceSourceTarget"), "ProjectReference", StringComparison.OrdinalIgnoreCase))
                        {
                            isProjectReference = true;

                            TaskItem projectReferenceToRemove = new TaskItem(existingReferenceItemForAssembly.ItemSpec);

                            projectReferencesToRemove.Add(projectReferenceToRemove);

                            continue;
                        }

                        TaskItem referenceToRemove = new TaskItem(existingReferenceItemForAssembly.ItemSpec);

                        referencesToRemove.Add(referenceToRemove);
                    }

                    if (isProjectReference)
                    {
                        TaskItem projectReferenceToAdd = new TaskItem(assemblyToRename.ShadedPath, existingReferenceItemsForAssembly.First().CloneCustomMetadata());

                        projectReferencesToAdd.Add(projectReferenceToAdd);
                    }
                    else
                    {
                        TaskItem referenceToAdd = new TaskItem(assemblyToRename.ShadedPath, existingReferenceItemsForAssembly.First().CloneCustomMetadata());

                        referenceToAdd.SetMetadata(ItemMetadataNames.HintPath, assemblyToRename.ShadedPath);
                        referenceToAdd.SetMetadata(ItemMetadataNames.OriginalPath, assemblyToRename.FullPath);

                        referencesToAdd.Add(referenceToAdd);
                    }
                }
                else
                {
                    TaskItem referenceToAdd = new TaskItem(assemblyToRename.ShadedPath);

                    referenceToAdd.SetMetadata(ItemMetadataNames.HintPath, assemblyToRename.ShadedPath);
                    referenceToAdd.SetMetadata(ItemMetadataNames.OriginalPath, assemblyToRename.FullPath);

                    //referencesToAdd.Add(referenceToAdd);
                }

                assembliesToShade.Add(assemblyToShade);
            }

            AssembliesToShade = assembliesToShade.ToArray();
            ReferencesToAdd = referencesToAdd.ToArray();
            ReferencesToRemove = referencesToRemove.ToArray();
            ProjectReferencesToAdd = projectReferencesToAdd.ToArray();
            ProjectReferencesToRemove = projectReferencesToRemove.ToArray();
        }
    }
}