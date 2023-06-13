using System.Collections.Generic;

namespace PackageShading.Tasks.UnitTests
{
    internal class MockNuGetAssetFileLoader : Dictionary<string, Dictionary<PackageIdentity, HashSet<PackageIdentity>>>, INuGetAssetFileLoader
    {
        public Dictionary<string, Dictionary<PackageIdentity, HashSet<PackageIdentity>>> LoadAssetsFile(string projectAssetsFile) => this;
    }
}