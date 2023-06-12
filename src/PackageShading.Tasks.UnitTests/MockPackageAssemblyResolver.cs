using System.Collections.Generic;
using System.Reflection;

namespace PackageShading.Tasks.UnitTests
{
    internal class MockPackageAssemblyResolver : Dictionary<PackageIdentity, List<(string Path, string Subdirectory, AssemblyName Name)>>, IPackageAssemblyResolver
    {
        public IEnumerable<(string Path, string Subdirectory, AssemblyName Name)> GetNearest(PackageIdentity packageIdentity, string nuGetPackageRoot, string targetFramework, string[] fallbackTargetFrameworks)
        {
            return this[packageIdentity];
        }
    }
}