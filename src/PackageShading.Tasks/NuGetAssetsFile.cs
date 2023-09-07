using System;
using System.Collections.Generic;

namespace PackageShading.Tasks
{
    public sealed class NuGetAssetsFile : Dictionary<string, NuGetAssetsFileSection>
    {
        public NuGetAssetsFile()
            : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        public Dictionary<string, PackageIdentity> ProjectReferences { get; } = new();
    }
}