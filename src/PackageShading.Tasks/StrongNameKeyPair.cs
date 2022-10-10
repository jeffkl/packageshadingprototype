using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace PackageShading.Tasks
{
    internal sealed class StrongNameKeyPair : System.Reflection.StrongNameKeyPair
    {
        private static byte[] _keyPairCache;

        private byte[] _publicKeyToken = null;

        private StrongNameKeyPair(FileStream keyPairFile)
            : base(keyPairFile)
        {
        }

        private StrongNameKeyPair(byte[] keyPairArray)
            : base(keyPairArray)
        {
        }

        private StrongNameKeyPair(string keyPairContainer)
            : base(keyPairContainer)
        {
        }

        public byte[] PublicKeyToken
        {
            get
            {
                if (_publicKeyToken == null)
                {
                    _publicKeyToken = GetPublicKeyToken();
                }

                return _publicKeyToken;
            }
        }

        public static StrongNameKeyPair Create(string keyPath = null, string keyFilePassword = null)
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

            using (RSACryptoServiceProvider provider = new RSACryptoServiceProvider(1024, new CspParameters() { KeyNumber = 2 }))
            {
                _keyPairCache = provider.ExportCspBlob(!provider.PublicOnly);
            }

            return new StrongNameKeyPair(_keyPairCache);
        }

        private byte[] GetPublicKeyToken()
        {
            if (PublicKey == null)
            {
                return null;
            }

            if (PublicKey.Length == 0)
            {
                return Array.Empty<byte>();
            }

            Span<byte> hash = stackalloc byte[20];

            Sha1ForNonSecretPurposes sha1 = default;
            sha1.Start();
            sha1.Append(PublicKey);
            sha1.Finish(hash);

            byte[] publicKeyToken = new byte[8];

            for (int i = 0; i < publicKeyToken.Length; i++)
            {
                publicKeyToken[i] = hash[hash.Length - 1 - i];
            }

            return publicKeyToken;
        }

        internal struct Sha1ForNonSecretPurposes
        {
            private long _length; // Total message length in bits
            private int _pos;
            private uint[] _w; // Workspace
                               // Length of current chunk in bytes

            public void Append(byte input)
            {
                int idx = _pos >> 2;
                _w[idx] = (_w[idx] << 8) | input;
                if (64 == ++_pos)
                {
                    Drain();
                }
            }

            public void Append(ReadOnlySpan<byte> input)
            {
                foreach (byte b in input)
                {
                    Append(b);
                }
            }

            public void Finish(Span<byte> output)
            {
                long l = _length + 8 * _pos;
                Append(0x80);
                while (_pos != 56)
                {
                    Append(0x00);
                }

                Append((byte)(l >> 56));
                Append((byte)(l >> 48));
                Append((byte)(l >> 40));
                Append((byte)(l >> 32));
                Append((byte)(l >> 24));
                Append((byte)(l >> 16));
                Append((byte)(l >> 8));
                Append((byte)l);

                int end = output.Length < 20 ? output.Length : 20;
                for (int i = 0; i != end; i++)
                {
                    uint temp = _w[80 + i / 4];
                    output[i] = (byte)(temp >> 24);
                    _w[80 + i / 4] = temp << 8;
                }
            }

            public void Start()
            {
                _w ??= new uint[85];

                _length = 0;
                _pos = 0;
                _w[80] = 0x67452301;
                _w[81] = 0xEFCDAB89;
                _w[82] = 0x98BADCFE;
                _w[83] = 0x10325476;
                _w[84] = 0xC3D2E1F0;
            }

            private void Drain()
            {
                for (int i = 16; i != 80; i++)
                {
                    _w[i] = RotateLeft(_w[i - 3] ^ _w[i - 8] ^ _w[i - 14] ^ _w[i - 16], 1);
                }

                uint a = _w[80];
                uint b = _w[81];
                uint c = _w[82];
                uint d = _w[83];
                uint e = _w[84];

                for (int i = 0; i != 20; i++)
                {
                    const uint k = 0x5A827999;
                    uint f = (b & c) | ((~b) & d);
                    uint temp = RotateLeft(a, 5) + f + e + k + _w[i]; e = d; d = c; c = RotateLeft(b, 30); b = a; a = temp;
                }

                for (int i = 20; i != 40; i++)
                {
                    uint f = b ^ c ^ d;
                    const uint k = 0x6ED9EBA1;
                    uint temp = RotateLeft(a, 5) + f + e + k + _w[i]; e = d; d = c; c = RotateLeft(b, 30); b = a; a = temp;
                }

                for (int i = 40; i != 60; i++)
                {
                    uint f = (b & c) | (b & d) | (c & d);
                    const uint k = 0x8F1BBCDC;
                    uint temp = RotateLeft(a, 5) + f + e + k + _w[i]; e = d; d = c; c = RotateLeft(b, 30); b = a; a = temp;
                }

                for (int i = 60; i != 80; i++)
                {
                    uint f = b ^ c ^ d;
                    const uint k = 0xCA62C1D6;
                    uint temp = RotateLeft(a, 5) + f + e + k + _w[i]; e = d; d = c; c = RotateLeft(b, 30); b = a; a = temp;
                }

                _w[80] += a;
                _w[81] += b;
                _w[82] += c;
                _w[83] += d;
                _w[84] += e;

                _length += 512; // 64 bytes == 512 bits
                _pos = 0;

                uint RotateLeft(uint value, int offset) => (value << offset) | (value >> (32 - offset));
            }
        }
    }
}