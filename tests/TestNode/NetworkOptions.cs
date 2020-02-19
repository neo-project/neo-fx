using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using NeoFx.Models;

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

        public async Task<(IPEndPoint, string)> GetRandomSeedAsync() 
        {
            var random = new Random();
            foreach (var seed in Seeds) //.OrderBy(_ => random.NextDouble()))
            {
                var (host, port) = ParseSeed(seed);
                var addresses = await Dns.GetHostAddressesAsync(host);
                if (addresses.Length > 0)
                {
                    var endPoint = new IPEndPoint(addresses[0], port);
                    return (endPoint, seed);
                }
            }

            throw new Exception("seed address not found");
        }

        public async IAsyncEnumerable<(IPEndPoint, string)> GetSeeds()
        {
            foreach (var seed in Seeds)
            {
                var (host, port) = ParseSeed(seed);
                var addresses = await Dns.GetHostAddressesAsync(host);
                if (addresses.Length > 0)
                {
                    var endPoint = new IPEndPoint(addresses[0], port);
                    yield return (endPoint, seed);
                }
            }
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

        public Block GetGenesisBlock()
        {
            return Genesis.CreateGenesisBlock(GetValidators());
        }
    }
}
