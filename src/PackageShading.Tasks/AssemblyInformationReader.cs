using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PackageShading.Tasks
{
    /// <summary>
    /// Represents a class that reads assembly references.
    /// </summary>
    public sealed class AssemblyInformationReader : IAssemblyInformationReader
    {
        public AssemblyInformation GetAssemblyInformation(IEnumerable<string> assemblyPaths)
        {
            AssemblyInformation assemblyInformation = new AssemblyInformation();

            using DefaultAssemblyResolver resolver = new DefaultAssemblyResolver();

            foreach (string assemblyPath in assemblyPaths)
            {
                FileInfo assemblyFileInfo = new FileInfo(assemblyPath);

                if (!assemblyFileInfo.Exists)
                {
                    continue;
                }

                resolver.AddSearchDirectory(assemblyFileInfo.DirectoryName);

                using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(assemblyFileInfo.FullName, new ReaderParameters
                {
                    AssemblyResolver = resolver,
                    ReadSymbols = File.Exists(Path.ChangeExtension(assemblyFileInfo.FullName, ".pdb"))
                });

                if (!assemblyInformation.AssemblyReferences.ContainsKey(assembly.FullName))
                {
                    assemblyInformation.AssemblyReferences.Add(assembly.FullName, new List<AssemblyReference>());
                }

                foreach (AssemblyNameReference assemblyReference in assembly.MainModule.AssemblyReferences)
                {
                    if (!assemblyInformation.AssemblyReferences.TryGetValue(assemblyReference.FullName, out List<AssemblyReference> assemblyReferences))
                    {
                        assemblyReferences = new List<AssemblyReference>();

                        assemblyInformation.AssemblyReferences.Add(assemblyReference.FullName, assemblyReferences);
                    }

                    assemblyReferences.Add(new AssemblyReference(assemblyFileInfo, assembly.Name.FullName));
                }

                foreach (CustomAttribute internalsVisibleToAttribute in assembly.CustomAttributes.Where(i => string.Equals(i.AttributeType.FullName, "System.Runtime.CompilerServices.InternalsVisibleToAttribute", StringComparison.Ordinal)))
                {
                    string value = internalsVisibleToAttribute.ConstructorArguments[0].Value as string;

                    if (!assemblyInformation.FriendAssemblies.TryGetValue(value, out List<AssemblyReference> assemblyReferences))
                    {
                        assemblyReferences = new List<AssemblyReference>();

                        assemblyInformation.FriendAssemblies.Add(value, assemblyReferences);
                    }

                    assemblyReferences.Add(new AssemblyReference(assemblyFileInfo, assembly.Name.FullName));
                }
            }

            return assemblyInformation;
        }
    }
}