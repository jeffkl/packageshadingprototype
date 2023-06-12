using System.Collections.Generic;
using System.Reflection;

namespace PackageShading.Tasks.UnitTests
{
    internal class MockAssemblyReferenceReader : Dictionary<string, List<(string AssemblyFullPath, AssemblyName AssemblyName)>>, IAssemblyReferenceReader
    {
        public Dictionary<string, List<(string AssemblyFullPath, AssemblyName AssemblyName)>> GetAssemblyReferences(IEnumerable<string> assemblyPaths) => this;
    }
}