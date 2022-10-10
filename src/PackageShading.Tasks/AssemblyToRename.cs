using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Mono.Cecil;

namespace PackageShading.Tasks
{
    /// <summary>
    /// Represents an assembly that will be renamed.
    /// </summary>
    [DebuggerDisplay("{AssemblyName, nq} => {ShadedAssemblyName,nq} ({ShadedPath,nq}}")]
    internal sealed class AssemblyToRename
    {
        public AssemblyToRename(ITaskItem taskItem)
        {
            if (taskItem != null)
            {
                FullPath = taskItem.ItemSpec;
                AssemblyName = AssemblyName.GetAssemblyName(FullPath);
                Metadata = taskItem.CloneCustomMetadata();
            }
        }

        public AssemblyToRename(string fullPath, ITaskItem taskItem = null)
            : this(taskItem)
        {
            FullPath = fullPath;
            AssemblyName = AssemblyName.GetAssemblyName(FullPath);
            if (Metadata == null)
            {
                Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public IDictionary Metadata { get; }

        public AssemblyName AssemblyName { get; }

        public AssemblyNameDefinition ShadedAssemblyName { get; set; }

        public string FullPath { get; }

        public string ShadedPath { get; set; }

        public bool IsReference { get; set; }

        public AssemblyDefinition ReadAssembly()
        {
            using DefaultAssemblyResolver resolver = new DefaultAssemblyResolver();

            resolver.AddSearchDirectory(Path.GetDirectoryName(FullPath));

            return AssemblyDefinition.ReadAssembly(FullPath, new ReaderParameters
            {
                AssemblyResolver = resolver,
                ReadSymbols = File.Exists(Path.ChangeExtension(FullPath, ".pdb"))
            });
        }
    }
}