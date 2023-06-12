using System.Collections.Generic;
using System.Reflection;

namespace PackageShading.Tasks
{
    /// <summary>
    /// Represents an interface for a class that resolves assemblies in a NuGet package.
    /// </summary>
    public interface IPackageAssemblyResolver
    {
        /// <summary>
        /// Gets the assembly paths for the specified package based on the supported target frameworks.
        /// </summary>
        /// <param name="packageIdentity">The <see cref="PackageIdentity" /> of the package.</param>
        /// <param name="nuGetPackageRoot">The root directory containing NuGet packages.</param>
        /// <param name="targetFramework">The target framework of the current project.</param>
        /// <param name="fallbackTargetFrameworks">An array of fallback target frameworks of the current project.</param>
        /// <returns></returns>
        IEnumerable<(string Path, string Subdirectory, AssemblyName Name)> GetNearest(PackageIdentity packageIdentity, string nuGetPackageRoot, string targetFramework, string[] fallbackTargetFrameworks);
    }
}