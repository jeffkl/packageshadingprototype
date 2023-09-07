using System.Collections.Generic;

namespace PackageShading.Tasks.UnitTests
{
    internal class MockNuGetAssetFileLoader : Dictionary<string, Dictionary<PackageIdentity, HashSet<PackageIdentity>>>, INuGetAssetFileLoader
    {
        public NuGetAssetsFile LoadAssetsFile(string projectDirectory, string projectAssetsFile)
        {
            NuGetAssetsFile assetsFile = new NuGetAssetsFile();

            foreach (var item in this)
            {
                foreach (var package in item.Value)
                {
                    assetsFile[item.Key].Packages[package.Key] = package.Value;
                }
            }

            return assetsFile;
        }
    }
}