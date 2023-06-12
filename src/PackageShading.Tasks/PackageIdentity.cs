using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PackageShading.Tasks
{
    /// <summary>
    /// Represents a NuGet package's identity.
    /// </summary>
    [DebuggerDisplay("{Id,nq}/{Version,nq}")]
    public struct PackageIdentity : IEqualityComparer<PackageIdentity>, IEquatable<PackageIdentity>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PackageIdentity" /> class with the specified ID and version.
        /// </summary>
        /// <param name="id">The ID of the package.</param>
        /// <param name="version">The version of the package.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public PackageIdentity(string id, string version)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentNullException(nameof(version));
            }

            Id = id;
            Version = version;
        }

        /// <summary>
        /// Gets the ID of the package.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the version of the package.
        /// </summary>
        public string Version { get; }

        public bool Equals(PackageIdentity x, PackageIdentity y) => string.Equals(x.Id, y.Id, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Version, y.Version, StringComparison.OrdinalIgnoreCase);

        public bool Equals(PackageIdentity other) => Id.Equals(other.Id, StringComparison.OrdinalIgnoreCase) && Version.Equals(other.Version, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(PackageIdentity obj) => HashCode.Combine(obj.Id, obj.Version);

        public override string ToString()
        {
            return $"{Id}/{Version}";
        }
    }
}