namespace PackageShading.Tasks
{
    /// <summary>
    /// Represents the names of MSBuild item metadata.
    /// </summary>
    internal static class ItemMetadataNames
    {
        /// <summary>
        /// The assembly name.
        /// </summary>
        public const string AssemblyName = nameof(AssemblyName);

        /// <summary>
        /// The destination subdirectory.
        /// </summary>
        public const string DestinationSubdirectory = nameof(DestinationSubdirectory);

        /// <summary>
        /// The full path to the assembly.
        /// </summary>
        public const string FullPath = nameof(FullPath);

        /// <summary>
        /// The hint path of the assembly.
        /// </summary>
        public const string HintPath = nameof(HintPath);

        /// <summary>
        /// The NuGet package version.
        /// </summary>
        public const string OriginalPath = nameof(OriginalPath);

        /// <summary>
        /// The shaded assembly name.
        /// </summary>
        public const string ShadedAssemblyName = nameof(ShadedAssemblyName);

        /// <summary>
        /// The list of dependencies to shade.
        /// </summary>
        public const string ShadeDependencies = nameof(ShadeDependencies);

        /// <summary>
        /// The version of a NuGet package.
        /// </summary>
        public const string Version = nameof(Version);
    }
}