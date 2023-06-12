using System.Collections.Generic;
using System.Reflection;

namespace PackageShading.Tasks
{
    /// <summary>
    /// Represents an interface for a class that reads assembly references.
    /// </summary>
    public interface IAssemblyReferenceReader
    {
        /// <summary>
        /// Gets the assembly references for the specified assemblies.
        /// </summary>
        /// <param name="assemblyPaths">An <see cref="IEnumerable{T}" /> containing full paths to the assemblies to get the assembly references of.</param>
        /// <returns>An <see cref="Dictionary{TKey, TValue}" /> containing assembly names and the list of assemblies that reference it.</returns>
        Dictionary<string, List<(string AssemblyFullPath, AssemblyName AssemblyName)>> GetAssemblyReferences(IEnumerable<string> assemblyPaths);
    }
}