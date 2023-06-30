using System.Collections.Generic;
using System.Reflection;

namespace PackageShading.Tasks
{
    /// <summary>
    /// Represents an interface for a class that reads assembly references.
    /// </summary>
    public interface IAssemblyInformationReader
    {
        AssemblyInformation GetAssemblyInformation(IEnumerable<string> assemblyPaths);
    }
}