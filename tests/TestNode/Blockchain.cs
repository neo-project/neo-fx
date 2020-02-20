using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoFx.Models;

namespace NeoFx.TestNode
{
    interface IBlockchain
    {
        ValueTask AddBlock(in Block block);
        ValueTask<(uint index, UInt256 hash)> GetLastBlockHash();
    }

    class Blockchain : IBlockchain
    {
        readonly NetworkOptions networkOptions;
        readonly ILogger<Blockchain> log;

        public Blockchain(IOptions<NetworkOptions> networkOptions,
            ILogger<Blockchain> logger)
        {
            this.networkOptions = networkOptions.Value;
            this.log = logger;
        }

        public ValueTask AddBlock(in Block block)
        {
            log.LogCritical("block {index} not really saved", block.Index);
            return default;
        }

        public ValueTask<(uint index, UInt256 hash)> GetLastBlockHash()
        {
            var genesis = Genesis.CreateGenesisBlock(GetValidators());
            return new ValueTask<(uint, UInt256)>((genesis.Index, genesis.CalculateHash()));
        }

        IEnumerable<ECPoint> GetValidators()
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

            var validators = networkOptions.Validators;
            for (int i = 0; i < validators.Length; i++)
            {
                if (TryConvertHexString(validators[i], out var bytes)
                    && (new EncodedPublicKey(bytes)).TryDecode(curve, out var point))
                {
                    yield return point;
                }
            }
        }
    }
}
