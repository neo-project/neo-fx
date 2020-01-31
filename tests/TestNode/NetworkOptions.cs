using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace NeoFx.TestNode
{
    public class NetworkOptions
    {
        public uint Magic { get; set; }
        public string[] Seeds { get; set; } = Array.Empty<string>();
        public string[] Validators { get; set; } = Array.Empty<string>();

        private (string address, int port) ParseSeed(string seed)
        {
            var colonIndex = seed.IndexOf(':');
            var address = seed.Substring(0, colonIndex);
            var port = int.Parse(seed.AsSpan().Slice(colonIndex + 1));
            return (address, port);
        }

        public IEnumerable<(string address, int port)> GetSeeds()
        {
            for (int i = 0; i < Seeds.Length; i++)
            {
                yield return ParseSeed(Seeds[i]);
            }
        }

        public (string address, int port) GetRandomSeed()
        {
            var random = new Random();
            return ParseSeed(Seeds[random.Next(0, Seeds.Length)]);
        }

        public IEnumerable<ECPoint> GetValidators()
        {
            static bool TryConvertHexString(string hex, out ImmutableArray<byte> value)
            {
                static int GetHexVal(char hex)
                {
                    return (int)hex - ((int)hex < 58 ? 48 : ((int)hex < 97 ? 55 : 87));
                }

                if (hex.Length % 2 == 0)
                {
                    var bytesLength = hex.Length >> 1;
                    var array = new byte[bytesLength];

                    for (int i = 0; i < bytesLength; ++i)
                    {
                        var charIndex = i << 1;
                        array[i] = (byte)((GetHexVal(hex[charIndex]) << 4) + (GetHexVal(hex[charIndex + 1])));
                    }

                    value = Unsafe.As<byte[], ImmutableArray<byte>>(ref array);
                    return true;
                }

                value = default;
                return false;
            }

            var curve = ECCurve.NamedCurves.nistP256.GetExplicit();

            for (int i = 0; i < Validators.Length; i++)
            {
                if (TryConvertHexString(Validators[i], out var bytes)
                    && (new EncodedPublicKey(bytes)).TryDecode(curve, out var point))
                {
                    yield return point;
                }
            }
        }

    }
}
