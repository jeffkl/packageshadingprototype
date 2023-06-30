using System;
using System.Collections.Generic;

namespace PackageShading.Tasks
{
    public class FriendAssembliesByInternalsVisibleTo : Dictionary<string, List<AssemblyReference>>
    {
        public FriendAssembliesByInternalsVisibleTo()
            : base(StringComparer.OrdinalIgnoreCase)
        {
        }
    }
}