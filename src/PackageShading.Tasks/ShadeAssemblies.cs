using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;

namespace PackageShading.Tasks
{
    public class ShadeAssemblies : Task
    {
        [Required]
        public ITaskItem[] References { get; set; }

        public string IntermediateOutputPath { get; set; }

        [Output]
        public ITaskItem[] ReferencesToUpdate { get; set; }

        public override bool Execute()
        {
            // As an optimization, loop through all of the references first and determine what they'll be renamed to.  This is
            // so that later on, we can process each assembly once

            IList<AssemblyToRename> assembliesToRename = new List<AssemblyToRename>();

            foreach (ITaskItem item in References)
            {
                string packageId = item.GetMetadata("NuGetPackageId");
                string sourceType = item.GetMetadata("NuGetSourceType");

                if (string.IsNullOrWhiteSpace(packageId) || !string.Equals(sourceType, "Package", StringComparison.OrdinalIgnoreCase))
                {
                    // Ignore Reference items that didn't come from a package.  Another option would be to allow a consumer to opt-out
                    // a single PackageReference from shading.  At the moment this shades EVERY PackageReference
                    continue;
                }

                AssemblyToRename assemblyToRename = new AssemblyToRename(item);

                // The new assembly file name includes the version to make it unique so Newtonsoft.Json.12.0.0.0.dll
                string assemblyName = $"{assemblyToRename.AssemblyName.Name}.{assemblyToRename.AssemblyName.Version}";

                assemblyToRename.ShadedAssemblyName = new AssemblyNameDefinition(assemblyName, assemblyToRename.AssemblyName.Version);

                // The location for the shaded assembly is under "obj"
                assemblyToRename.ShadedPath = Path.GetFullPath(Path.Combine(IntermediateOutputPath, "ShadedAssemblies", $"{assemblyName}.dll"));
                
                Log.LogMessageFromText($"Shading assembly reference {assemblyToRename.FullPath} => {assemblyToRename.ShadedPath}", MessageImportance.High);

                assembliesToRename.Add(assemblyToRename);
            }

            List<ITaskItem> referencesToUpdate = new List<ITaskItem>(assembliesToRename.Count);

            // Generate a strong name key pair on the fly, we could also use the SNK that the project is
            System.Reflection.StrongNameKeyPair strongNameKeyPair = StrongNameKeyPair.Create();

            // Now loop through each assembly to process, this could probably be done in parallel if needed
            foreach (AssemblyToRename assemblyToRename in assembliesToRename)
            {
                // Read in the source assembly
                using (AssemblyDefinition assembly = assemblyToRename.ReadAssembly())
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(assemblyToRename.ShadedPath));

                    // This renames the assembly "name", not the file name
                    assembly.Name.Name = assemblyToRename.ShadedAssemblyName.Name;

                    // Remove the strong name signature flag, this is updated if we add a strong name later
                    assembly.MainModule.Attributes &= ~ModuleAttributes.StrongNameSigned;

                    // You must write an assembly with a strong name in order to get its public key token so this is
                    // writing it to disk in case we need the public key token later on
                    assembly.Write(
                        assemblyToRename.ShadedPath,
                        new WriterParameters
                        {
                            StrongNameKeyPair = strongNameKeyPair,
                        });

                    bool dirty = false;
                    // Loop through the assembly references to find any that need to be updated
                    foreach (AssemblyNameReference assemblyReference in assembly.MainModule.AssemblyReferences)
                    {
                        // Find a renamed assembly that matches the reference
                        AssemblyToRename newReference = assembliesToRename.FirstOrDefault(i => assemblyReference.FullName == i.AssemblyName.FullName);

                        if (newReference != null)
                        {
                            Log.LogMessageFromText($"  - {assemblyReference.FullName} -> {newReference.ShadedAssemblyName.FullName}", MessageImportance.High);

                            // Update the reference name and public key token
                            assemblyReference.Name = newReference.ShadedAssemblyName.Name;
                            assemblyReference.PublicKeyToken = assembly.Name.PublicKeyToken;

                            dirty = true;
                        }
                    }

                    // Write the assembly one more time if any references were updated
                    if (dirty)
                    {
                        assembly.Write(
                            assemblyToRename.ShadedPath,
                            new WriterParameters
                            {
                                StrongNameKeyPair = strongNameKeyPair,
                            });
                    }
                }

                // This just verifies that the assembly isn't broken.  If it is, ResolveAssemblyReferences fails later on, I just put
                // this here so I could debug it
                AssemblyName _ = AssemblyName.GetAssemblyName(assemblyToRename.ShadedPath);

                // Create an item to return that represents the shaded assembly, later on it the original will be replaced with this one
                TaskItem referenceToUpdate = new TaskItem(Path.GetFullPath(assemblyToRename.ShadedPath), assemblyToRename.Metadata);
                
                referenceToUpdate.SetMetadata("HintPath", referenceToUpdate.ItemSpec);
                // Metadata to maybe used later saying its a shaded assembly
                referenceToUpdate.SetMetadata("IsShadedAssembly", bool.TrueString);
                referenceToUpdate.SetMetadata("AssetType", "runtime");

                referencesToUpdate.Add(referenceToUpdate);
            }

            ReferencesToUpdate = referencesToUpdate.ToArray();

            return !Log.HasLoggedErrors;
        }
    }
}
