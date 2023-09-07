using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PackageShading.Tasks
{
    internal class NuGetAssetFileLoader : INuGetAssetFileLoader
    {
        public NuGetAssetsFile LoadAssetsFile(string projectDirectory, string projectAssetsFile)
        {
            NuGetAssetsFile assetsFile = new NuGetAssetsFile();

            JsonDocumentOptions options = new JsonDocumentOptions
            {
                AllowTrailingCommas = true
            };

            using (FileStream stream = File.OpenRead(projectAssetsFile))
            {
                using (JsonDocument json = JsonDocument.Parse(stream, options))
                {
                    foreach (JsonProperty targetFramework in json.RootElement.GetProperty("targets").EnumerateObject())
                    {
                        NuGetAssetsFileSection nuGetAssetsFileSection = new NuGetAssetsFileSection();

                        Dictionary<PackageIdentity, HashSet<PackageIdentity>> packages = nuGetAssetsFileSection.Packages;

                        if (targetFramework.Value.ValueKind == JsonValueKind.Undefined)
                        {
                            continue;
                        }

                        foreach (JsonProperty item in targetFramework.Value.EnumerateObject())
                        {
                            string[] packageDetails = item.Name.Split('/');

                            if (!NuGetVersion.TryParse(packageDetails[1], out NuGetVersion nuGetVersion))
                            {
                                continue;
                            }

                            PackageIdentity packageIdentity = new PackageIdentity(packageDetails[0], packageDetails[1]);

                            if (!packages.TryGetValue(packageIdentity, out HashSet<PackageIdentity> dependencies))
                            {
                                dependencies = new HashSet<PackageIdentity>();

                                packages[packageIdentity] = dependencies;
                            }

                            if (item.Value.TryGetProperty("dependencies", out JsonElement deps))
                            {
                                foreach (JsonProperty dependency in deps.EnumerateObject())
                                {
                                    string versionString = dependency.Value.GetString();

                                    if (!VersionRange.TryParse(versionString, out VersionRange versionRange))
                                    {
                                        continue;
                                    }

                                    dependencies.Add(new PackageIdentity(dependency.Name, versionRange.MinVersion.ToNormalizedString()));
                                }
                            }
                        }

                        bool added = false;
                        int count = 0;
                        do
                        {
                            count++;
                            added = false;

                            foreach (KeyValuePair<PackageIdentity, HashSet<PackageIdentity>> package in packages)
                            {
                                List<PackageIdentity> dependenciesToAdd = new List<PackageIdentity>();

                                foreach (PackageIdentity dependency in package.Value)
                                {
                                    if (packages.TryGetValue(dependency, out HashSet<PackageIdentity> dependencies))
                                    {
                                        dependenciesToAdd.AddRange(dependencies);
                                    }
                                }

                                foreach (PackageIdentity dependencyToAdd in dependenciesToAdd)
                                {
                                    if (package.Value.Add(dependencyToAdd))
                                    {
                                        added = true;
                                    }
                                }
                            }
                        }
                        while (added);

                        assetsFile[targetFramework.Name] = nuGetAssetsFileSection;
                    }

                    foreach (JsonProperty library in json.RootElement.GetProperty("libraries").EnumerateObject())
                    {
                        if (!library.Value.TryGetProperty("type", out JsonElement type) || !string.Equals(type.GetString(), "project") || !library.Value.TryGetProperty("path", out JsonElement path))
                        {
                            continue;
                        }

                        string[] libraryDetails = library.Name.Split('/');

                        PackageIdentity packageIdentity = new PackageIdentity(libraryDetails[0], libraryDetails[1]);

                        string relativePath = path.GetString();

                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            relativePath = relativePath.Replace('/', '\\');
                        }

                        FileInfo projectFileInfo = new FileInfo(Path.Combine(projectDirectory, relativePath));

                        assetsFile.ProjectReferences[projectFileInfo.FullName] = packageIdentity;
                    }
                }
            }

            return assetsFile;
        }
    }
}