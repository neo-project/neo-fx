using DevHawk.Buffers;
using NeoFx.Storage;
using System.Collections.Immutable;
using System.Diagnostics;

namespace NeoFx.Models
{
    public readonly struct Account
    {
        public readonly UInt160 ScriptHash;
        public readonly bool IsFrozen;
        public readonly ImmutableArray<EncodedPublicKey> Votes;
        public readonly ImmutableDictionary<UInt256, Fixed8> Balances;

        public Account(UInt160 scriptHash, bool isFrozen, ImmutableArray<EncodedPublicKey> votes, ImmutableDictionary<UInt256, Fixed8> balances)
        {
            ScriptHash = scriptHash;
            IsFrozen = isFrozen;
            Votes = votes == default ? ImmutableArray.Create<EncodedPublicKey>() : votes;
            Balances = balances ?? ImmutableDictionary<UInt256, Fixed8>.Empty;
        }

        public Account(UInt160 scriptHash)
            : this(scriptHash, false, default, ImmutableDictionary<UInt256, Fixed8>.Empty)
        {
        }

        public static bool TryRead(ref BufferReader<byte> reader, out Account value)
        {
            if (UInt160.TryRead(ref reader, out var scriptHash)
                && reader.TryRead(out byte isFrozen)
                && reader.TryReadVarArray<EncodedPublicKey>(EncodedPublicKey.TryRead, out var votes)
                && reader.TryReadVarInt(out var balancesCount))
            {
                Debug.Assert(balancesCount < int.MaxValue);

                var builder = ImmutableDictionary.CreateBuilder<UInt256, Fixed8>();
                for (var i = 0; i < (int)balancesCount; i++)
                {
                    if (UInt256.TryRead(ref reader, out var assetId)
                        && Fixed8.TryRead(ref reader, out var amount))
                    {
                        builder.Add(assetId, amount);
                    }
                    else
                    {
                        value = default;
                        return false;
                    }
                }

                value = new Account(scriptHash, isFrozen != 0, votes, builder.ToImmutable());
                return true;
            }

            value = default;
            return false;
        }

    }
}
