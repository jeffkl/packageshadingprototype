using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PackageShading.Tasks
{
    /// <inheritdoc />
    internal class NuGetAssetFileLoader : INuGetAssetFileLoader
    {
        /// <inheritdoc />
        public Dictionary<string, Dictionary<PackageIdentity, HashSet<PackageIdentity>>> LoadAssetsFile(string projectAssetsFile)
        {
            Dictionary<string, Dictionary<PackageIdentity, HashSet<PackageIdentity>>> packagesByTargetFramework = new Dictionary<string, Dictionary<PackageIdentity, HashSet<PackageIdentity>>>(StringComparer.OrdinalIgnoreCase);

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
                        Dictionary<PackageIdentity, HashSet<PackageIdentity>> packages = new Dictionary<PackageIdentity, HashSet<PackageIdentity>>();

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

                        packagesByTargetFramework[targetFramework.Name] = packages;
                    }
                }
            }

            return packagesByTargetFramework;
        }
    }
}