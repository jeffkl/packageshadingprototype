using System.Collections.Generic;

namespace PackageShading.Tasks
{
    public sealed class NuGetAssetsFileSection
    {
        public Dictionary<PackageIdentity, HashSet<PackageIdentity>> Packages { get; } = new Dictionary<PackageIdentity, HashSet<PackageIdentity>>();
    }
}