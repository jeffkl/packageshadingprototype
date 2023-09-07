using System.Collections.Generic;

namespace PackageShading.Tasks
{
    /// <summary>
    /// Represents an interface for a class that loads NuGet assets files.
    /// </summary>
    public interface INuGetAssetFileLoader
    {
        /// <summary>
        /// Loads the specified NuGet assets file.
        /// </summary>
        /// <param name="projectAssetsFile">The full path to the NuGet assests file to load.</param>
        /// <returns>An <see cref="Dictionary{TKey, TValue}" /> containing target frameworks and a list of packages with their dependencies.</returns>
        NuGetAssetsFile LoadAssetsFile(string projectDirectory, string projectAssetsFile);
    }
}