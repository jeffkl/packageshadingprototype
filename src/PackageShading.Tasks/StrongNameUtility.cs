using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace PackageShading.Tasks
{
    internal static class StrongNameUtility
    {
        private static byte[] _keyPairCache;

        public static StrongNameKeyPair GetStrongNameKeyPair(string keyPath = null, string keyFilePassword = null)
        {
            if (!string.IsNullOrEmpty(keyPath))
            {
                if (!string.IsNullOrEmpty(keyFilePassword))
                {
                    X509Certificate2 certificate = new X509Certificate2(keyPath, keyFilePassword, X509KeyStorageFlags.Exportable);

                    if (certificate.PrivateKey is not RSACryptoServiceProvider provider)
                    {
                        throw new InvalidOperationException("The key file is not password protected or the incorrect password was provided.");
                    }

                    return new StrongNameKeyPair(provider.ExportCspBlob(true));
                }

                return new StrongNameKeyPair(File.ReadAllBytes(keyPath));
            }

            if (_keyPairCache != null)
            {
                return new StrongNameKeyPair(_keyPairCache);
            }

            using (var provider = new RSACryptoServiceProvider(1024, new CspParameters() { KeyNumber = 2 }))
            {
                _keyPairCache = provider.ExportCspBlob(!provider.PublicOnly);
            }

            return new StrongNameKeyPair(_keyPairCache);
        }
    }
}
