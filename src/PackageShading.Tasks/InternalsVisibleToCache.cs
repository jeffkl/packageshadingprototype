using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PackageShading.Tasks
{
    internal class InternalsVisibleToCache
    {
        private static readonly ConcurrentDictionary<AssemblyName, Lazy<string>> Cache = new ConcurrentDictionary<AssemblyName, Lazy<string>>(AssemblyNameComparer.Instance);

        public static string GetInternalsVisibleTo(AssemblyName assemblyName) => Cache.GetOrAdd(assemblyName, new Lazy<string>(() =>
        {
            if (assemblyName.Flags.HasFlag(AssemblyNameFlags.PublicKey))
            {
                return $"{assemblyName.Name}, {string.Join(string.Empty, assemblyName.GetPublicKey().Select(i => i.ToString("x2")))}";
            }

            return assemblyName.Name;
        })).Value;

        private class AssemblyNameComparer : IEqualityComparer<AssemblyName>
        {
            public static readonly AssemblyNameComparer Instance = new AssemblyNameComparer();

            public bool Equals(AssemblyName x, AssemblyName y)
            {
                if (x == null || y == null)
                {
                    return false;
                }

                return string.Equals(x.FullName, y.FullName);
            }

            public int GetHashCode(AssemblyName obj)
            {
                return obj.FullName.GetHashCode();
            }
        }
    }
}