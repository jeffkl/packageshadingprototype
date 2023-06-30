using System;
using System.Collections.Generic;
using System.Reflection;

namespace PackageShading.Tasks.UnitTests
{
    internal class MockAssemblyInformationReader : Dictionary<string, List<AssemblyReference>>, IAssemblyInformationReader
    {
        public MockAssemblyInformationReader()
            : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        public AssemblyInformation GetAssemblyInformation(IEnumerable<string> assemblyPaths)
        {
            AssemblyInformation assemblyInformation = new AssemblyInformation();

            foreach (var i in this)
            {
                assemblyInformation.AssemblyReferences.Add(i.Key, i.Value);
            }

            return assemblyInformation;
        }
    }
}