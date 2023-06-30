using Mono.Cecil;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace PackageShading.Tasks
{
    internal class InternalsVisibleToCache
    {
        private static readonly ConcurrentDictionary<string, Lazy<string>> Cache = new ConcurrentDictionary<string, Lazy<string>>(StringComparer.OrdinalIgnoreCase);

        public static string GetInternalsVisibleTo(string path) => Cache.GetOrAdd(path, new Lazy<string>(() =>
        {
            using AssemblyDefinition assemblyDefinition = AssemblyDefinition.ReadAssembly(path);

            if (assemblyDefinition.Name.HasPublicKey)
            {
                return $"{assemblyDefinition.Name.Name}, PublicKey={string.Join(string.Empty, assemblyDefinition.Name.PublicKey.Select(i => i.ToString("x2")))}";
            }

            return assemblyDefinition.Name.Name;
        })).Value;
    }
}