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
        private static readonly ConcurrentDictionary<string, Lazy<Dictionary<string, Dictionary<PackageIdentity, HashSet<PackageIdentity>>>>> AssetsFileCache = new ConcurrentDictionary<string, Lazy<Dictionary<string, Dictionary<PackageIdentity, HashSet<PackageIdentity>>>>>(StringComparer.OrdinalIgnoreCase);
        private static readonly char[] SplitChars = new[] { ';', ',' };
        private readonly IAssemblyReferenceReader _assemblyReferenceReader;
        private readonly INuGetAssetFileLoader _nuGetAssetFileLoader;
        private readonly IPackageAssemblyResolver _packageAssemblyResolver;

        public GetAssembliesToShade()
            : this(new NuGetAssetFileLoader(), new PackageAssemblyResolver(), new AssemblyReferenceReader())
        {
        }

        public GetAssembliesToShade(INuGetAssetFileLoader nuGetAssetFileLoader, IPackageAssemblyResolver packageAssemblyResolver, IAssemblyReferenceReader assemblyReferenceReader)
        {
            _nuGetAssetFileLoader = nuGetAssetFileLoader ?? throw new ArgumentNullException(nameof(nuGetAssetFileLoader));
            _packageAssemblyResolver = packageAssemblyResolver ?? throw new ArgumentNullException(nameof(packageAssemblyResolver));
            _assemblyReferenceReader = assemblyReferenceReader ?? throw new ArgumentNullException(nameof(assemblyReferenceReader));
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

            Lazy<Dictionary<string, Dictionary<PackageIdentity, HashSet<PackageIdentity>>>> assetsFileLazy = AssetsFileCache.GetOrAdd(ProjectAssetsFile, assetsFile => new Lazy<Dictionary<string, Dictionary<PackageIdentity, HashSet<PackageIdentity>>>>(() => _nuGetAssetFileLoader.LoadAssetsFile(assetsFile)));

            Dictionary<string, Dictionary<PackageIdentity, HashSet<PackageIdentity>>> packagesByTargetFramework = assetsFileLazy.Value;

            if (packagesByTargetFramework == null)
            {
                return !Log.HasLoggedErrors;
            }

            if (!packagesByTargetFramework.TryGetValue(TargetFramework, out Dictionary<PackageIdentity, HashSet<PackageIdentity>> packages) && !packagesByTargetFramework.TryGetValue(TargetFrameworkMoniker, out packages))
            {
                return !Log.HasLoggedErrors;
            }

            HashSet<PackageIdentity> packagesToShade = GetPackagesToShade(packages);

            if (!packagesToShade.Any())
            {
                return !Log.HasLoggedErrors;
            }

            List<AssemblyToRename> assembliesToRename = GetAssembliesToShadeForPackages(strongNameKeyPair, packagesToShade);

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
            Dictionary<string, List<(string AssemblyFullPath, AssemblyName AssemblyName)>> assemblyReferencesByAssemblyName = _assemblyReferenceReader.GetAssemblyReferences(References.Select(i => i.GetMetadata(ItemMetadataNames.FullPath)));

            Stack<AssemblyToRename> stack = new Stack<AssemblyToRename>(assembliesToRename);

            while (stack.Any())
            {
                AssemblyToRename value = stack.Pop();

                if (!assemblyReferencesByAssemblyName.TryGetValue(value.AssemblyName.FullName, out List<(string AssemblyFullPath, AssemblyName AssemblyName)> assemblyReferences))
                {
                    continue;
                }

                foreach ((string assemblyFullPath, AssemblyName assemblyName) in assemblyReferences)
                {
                    if (!assemblyReferencesByAssemblyName.ContainsKey(assemblyName.FullName))
                    {
                        continue;
                    }

                    if (assembliesToRename.Any(i => string.Equals(i.AssemblyName.FullName, assemblyName.FullName)))
                    {
                        continue;
                    }

                    AssemblyToRename assemblyToRename = new AssemblyToRename(assemblyFullPath, assemblyName)
                    {
                        ShadedAssemblyName = new AssemblyNameDefinition(assemblyName.Name, assemblyName.Version)
                        {
                            Culture = assemblyName.CultureName,
                            PublicKey = strongNameKeyPair.PublicKey,
                        },
                        ShadedPath = Path.GetFullPath(Path.Combine(IntermediateOutputPath, "ShadedAssemblies", Path.GetFileName(assemblyFullPath)))
                    };

                    assemblyToRename.InternalsVisibleTo = InternalsVisibleToCache.GetInternalsVisibleTo(assemblyName);
                    assemblyToRename.ShadedInternalsVisibleTo = $"{assemblyToRename.ShadedAssemblyName.Name}, PublicKey={strongNameKeyPair.PublicKeyString}";

                    assembliesToRename.Add(assemblyToRename);

                    stack.Push(assemblyToRename);

                    assembliesToRename.AddRange(GetResourceAssemblies(strongNameKeyPair, assemblyToRename));
                }
            }
        }

        private List<AssemblyToRename> GetAssembliesToShadeForPackages(StrongNameKeyPair strongNameKeyPair, HashSet<PackageIdentity> packagesToShade)
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
                        continue;
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

                        assemblyToRename.InternalsVisibleTo = InternalsVisibleToCache.GetInternalsVisibleTo(assemblyFile.Name);
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

        private HashSet<PackageIdentity> GetPackagesToShade(Dictionary<PackageIdentity, HashSet<PackageIdentity>> packages)
        {
            HashSet<PackageIdentity> packagesToShade = new HashSet<PackageIdentity>();

            Dictionary<string, string> packageVersions = PackageVersions != null && PackageVersions.Any() ? PackageVersions.ToDictionary(i => i.ItemSpec, i => i.GetMetadata(ItemMetadataNames.Version), StringComparer.OrdinalIgnoreCase) : null;

            foreach (ITaskItem packageReference in PackageReferences)
            {
                string shadeDependencies = packageReference.GetMetadata(ItemMetadataNames.ShadeDependencies);

                if (string.IsNullOrWhiteSpace(shadeDependencies))
                {
                    continue;
                }
                
                PackageIdentity packageIdentity = new PackageIdentity(packageReference.ItemSpec, GetPackageVersion(packageReference, packageVersions));

                if (packages.TryGetValue(packageIdentity, out HashSet<PackageIdentity> dependencies))
                {
                    foreach (string shadeDependency in shadeDependencies.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries))
                    {
                        PackageIdentity packageToShade = dependencies.FirstOrDefault(i => string.Equals(i.Id, shadeDependency, StringComparison.OrdinalIgnoreCase));

                        packagesToShade.Add(packageToShade);
                    }
                }
            }

            Stack<PackageIdentity> packagesStack = new Stack<PackageIdentity>(packagesToShade);

            while (packagesStack.Any())
            {
                PackageIdentity packageToShade = packagesStack.Pop();

                if (packages.TryGetValue(packageToShade, out HashSet<PackageIdentity> dependencies))
                {
                    foreach (PackageIdentity dependency in dependencies)
                    {
                        if (!packagesToShade.Contains(dependency))
                        {
                            packagesToShade.Add(dependency);
                            packagesStack.Push(dependency);
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
                    foreach (ITaskItem existingReferenceItemForAssembly in existingReferenceItemsForAssembly)
                    {
                        TaskItem referenceToRemove = new TaskItem(existingReferenceItemForAssembly.ItemSpec);

                        referencesToRemove.Add(referenceToRemove);
                    }

                    TaskItem referenceToAdd = new TaskItem(assemblyToRename.ShadedPath, existingReferenceItemsForAssembly.First().CloneCustomMetadata());

                    referenceToAdd.SetMetadata(ItemMetadataNames.HintPath, assemblyToRename.ShadedPath);
                    referenceToAdd.SetMetadata(ItemMetadataNames.OriginalPath, assemblyToRename.FullPath);

                    referencesToAdd.Add(referenceToAdd);
                }

                assembliesToShade.Add(assemblyToShade);
            }

            AssembliesToShade = assembliesToShade.ToArray();
            ReferencesToAdd = referencesToAdd.ToArray();
            ReferencesToRemove = referencesToRemove.ToArray();
        }
    }
}