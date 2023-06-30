using System.IO;
using System.Reflection;

namespace PackageShading.Tasks
{
    public sealed class AssemblyReference
    {
        public AssemblyReference(string fullPath, AssemblyName name)
        {
            FullPath = fullPath;
            Name = name;
        }

        public AssemblyReference(string fullPath)
            : this(fullPath, AssemblyNameCache.GetAssemblyName(fullPath))
        {
        }

        public AssemblyReference(FileInfo fullPath, string assemblyName)
            : this(fullPath.FullName, new AssemblyName(assemblyName))
        {
        }

        public string FullPath { get; private set; }

        public AssemblyName Name { get; private set; }
    }
}