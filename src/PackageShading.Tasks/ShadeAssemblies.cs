using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PackageShading.Tasks
{
    /// <summary>
    /// Represents an MSBuild task that shades assemblies.
    /// </summary>
    public class ShadeAssemblies : Task
    {
        /// <summary>
        /// Gets or sets an array of <see cref="ITaskItem" /> objects representing the assemblies to shade.
        /// </summary>
        [Required]
        public ITaskItem[] AssembliesToShade { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the debugger should be launched when the task is executed.
        /// </summary>
        public bool Debug { get; set; }

        /// <summary>
        /// Gets or sets the full path to the key file used to sign shaded assemblies.
        /// </summary>
        [Required]
        public string ShadedAssemblyKeyFile { get; set; }

        public override bool Execute()
        {
            if (Debug)
            {
                Debugger.Launch();
            }

            StrongNameKeyPair strongNameKeyPair = new StrongNameKeyPair(ShadedAssemblyKeyFile);

            Dictionary<string, AssemblyName> newAssemblyNames = AssembliesToShade.ToDictionary(i => i.GetMetadata(ItemMetadataNames.AssemblyName), i => new AssemblyName(i.GetMetadata(ItemMetadataNames.ShadedAssemblyName)), StringComparer.OrdinalIgnoreCase);

            foreach (ITaskItem assemblyToShade in AssembliesToShade)
            {
                FileInfo assemblyPath = new FileInfo(assemblyToShade.GetMetadata(ItemMetadataNames.OriginalPath));
                FileInfo shadedAssemblyPath = new FileInfo(assemblyToShade.ItemSpec);

                AssemblyName shadedAssemblyName = new AssemblyName(assemblyToShade.GetMetadata(ItemMetadataNames.ShadedAssemblyName));

                using (AssemblyDefinition assembly = ReadAssembly(assemblyPath))
                {
                    string previousAssemblyName = assembly.Name.FullName;

                    Directory.CreateDirectory(shadedAssemblyPath.DirectoryName);

                    assembly.Name.Name = shadedAssemblyName.Name;

                    assembly.MainModule.Attributes &= ~ModuleAttributes.StrongNameSigned;

                    Log.LogMessageFromText($"Shading assembly {assemblyPath.FullName} => {shadedAssemblyPath.FullName}", MessageImportance.Normal);
                    Log.LogMessageFromText($"  Name: {previousAssemblyName} => {shadedAssemblyName.FullName}", MessageImportance.Normal);

                    foreach (AssemblyNameReference assemblyReference in assembly.MainModule.AssemblyReferences)
                    {
                        if (newAssemblyNames.TryGetValue(assemblyReference.FullName, out AssemblyName shadedReferenceAssemblyName))
                        {
                            Log.LogMessageFromText($"  Reference: {assemblyReference.FullName} -> {shadedReferenceAssemblyName.FullName}", MessageImportance.Normal);

                            assemblyReference.Name = shadedReferenceAssemblyName.Name;
                            assemblyReference.PublicKeyToken = strongNameKeyPair.PublicKeyToken;
                        }
                    }

                    assembly.Write(
                        shadedAssemblyPath.FullName,
                        new WriterParameters
                        {
                            StrongNameKeyBlob = strongNameKeyPair.PublicKey,
                        });
                }

                AssemblyName _ = AssemblyName.GetAssemblyName(shadedAssemblyPath.FullName);
            }

            return !Log.HasLoggedErrors;
        }

        private AssemblyDefinition ReadAssembly(FileInfo assemblyPath)
        {
            using DefaultAssemblyResolver resolver = new DefaultAssemblyResolver();

            resolver.AddSearchDirectory(assemblyPath.DirectoryName);

            return AssemblyDefinition.ReadAssembly(assemblyPath.FullName, new ReaderParameters
            {
                AssemblyResolver = resolver,
                ReadSymbols = File.Exists(Path.ChangeExtension(assemblyPath.FullName, ".pdb"))
            });
        }
    }
}