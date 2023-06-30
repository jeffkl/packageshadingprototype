using System;
using System.Collections.Generic;

namespace PackageShading.Tasks
{
    public sealed class AssemblyReferencesByAssemblyName : Dictionary<string, List<AssemblyReference>>
    {
        public AssemblyReferencesByAssemblyName()
            : base(StringComparer.OrdinalIgnoreCase)
        {
        }
    }
}