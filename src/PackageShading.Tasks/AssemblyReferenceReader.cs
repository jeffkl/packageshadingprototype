using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace PackageShading.Tasks
{
    /// <summary>
    /// Represents a class that reads assembly references.
    /// </summary>
    public sealed class AssemblyReferenceReader : IAssemblyReferenceReader
    {
        public Dictionary<string, List<(string AssemblyFullPath, AssemblyName AssemblyName)>> GetAssemblyReferences(IEnumerable<string> assemblyPaths)
        {
            Dictionary<string, List<(string AssemblyFullPath, AssemblyName AssemblyName)>> assemblyReferencesByAssemblyName = new Dictionary<string, List<(string AssemblyFullPath, AssemblyName AssemblyName)>>(StringComparer.OrdinalIgnoreCase);

            using DefaultAssemblyResolver resolver = new DefaultAssemblyResolver();

            foreach (string assemblyPath in assemblyPaths)
            {
                FileInfo assemblyFileInfo = new FileInfo(assemblyPath);

                resolver.AddSearchDirectory(assemblyFileInfo.DirectoryName);

                using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(assemblyFileInfo.FullName, new ReaderParameters
                {
                    AssemblyResolver = resolver,
                    ReadSymbols = File.Exists(Path.ChangeExtension(assemblyFileInfo.FullName, ".pdb"))
                });

                if (!assemblyReferencesByAssemblyName.ContainsKey(assembly.FullName))
                {
                    assemblyReferencesByAssemblyName.Add(assembly.FullName, new List<(string AssemblyFullPath, AssemblyName AssemblyName)>());
                }

                foreach (AssemblyNameReference assemblyReference in assembly.MainModule.AssemblyReferences)
                {
                    if (!assemblyReferencesByAssemblyName.TryGetValue(assemblyReference.FullName, out List<(string AssemblyFullPath, AssemblyName AssemblyName)> assemblyReferences))
                    {
                        assemblyReferences = new List<(string AssemblyFullPath, AssemblyName AssemblyName)>();

                        assemblyReferencesByAssemblyName.Add(assemblyReference.FullName, assemblyReferences);
                    }

                    assemblyReferences.Add((assemblyFileInfo.FullName, new AssemblyName(assembly.Name.FullName)));
                }
            }

            return assemblyReferencesByAssemblyName;
        }
    }
}