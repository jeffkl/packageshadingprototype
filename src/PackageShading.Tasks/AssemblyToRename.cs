using System.Collections;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Mono.Cecil;

namespace PackageShading.Tasks
{
    /// <summary>
    /// Represents an assembly that will be renamed.
    /// </summary>
    internal sealed class AssemblyToRename
    {
        public AssemblyToRename(ITaskItem taskItem)
        {
            FullPath = taskItem.ItemSpec;
            AssemblyName = AssemblyName.GetAssemblyName(FullPath);
            Metadata = taskItem.CloneCustomMetadata();
        }

        public IDictionary Metadata { get; }

        public AssemblyName AssemblyName { get; }

        public AssemblyNameDefinition ShadedAssemblyName { get; set; }

        public string FullPath { get; }

        public string ShadedPath { get; set; }

        public AssemblyDefinition ReadAssembly()
        {
            using var resolver = new DefaultAssemblyResolver();

            resolver.AddSearchDirectory(Path.GetDirectoryName(FullPath));

            return AssemblyDefinition.ReadAssembly(FullPath, new ReaderParameters
            {
                AssemblyResolver = resolver,
                ReadSymbols = File.Exists(Path.ChangeExtension(FullPath, ".pdb"))
            });
        }
    }
}