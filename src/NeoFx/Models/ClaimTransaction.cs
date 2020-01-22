using DevHawk.Buffers;
using NeoFx.Storage;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NeoFx.Models
{
    public sealed class ClaimTransaction : Transaction
    {
        public readonly ImmutableArray<CoinReference> Claims;

        public ClaimTransaction(ImmutableArray<CoinReference> claims,
                                byte version,
                                IEnumerable<TransactionAttribute> attributes,
                                IEnumerable<CoinReference> inputs,
                                IEnumerable<TransactionOutput> outputs,
                                IEnumerable<Witness> witnesses)
            : base(version, attributes, inputs, outputs, witnesses)
        {
            Claims = claims;
        }

        private ClaimTransaction(ImmutableArray<CoinReference> claims,
                                 byte version,
                                 in CommonData commonData)
            : base(version, commonData)
        {
            Claims = claims;
        }

        public static bool TryRead(ref BufferReader<byte> reader, byte version, [NotNullWhen(true)] out ClaimTransaction? tx)
        {
            if (reader.TryReadVarArray<CoinReference>(out var claims)
                && TryReadCommonData(ref reader, out var commonData))
            {
                tx = new ClaimTransaction(claims, version, commonData);
                return true;
            }

            tx = null;
            return false;
        }

        public override void WriteTransactionData(ref BufferWriter<byte> writer)
        {
            writer.Write((byte)TransactionType.Claim);
            writer.WriteLittleEndian(Version);
            writer.WriteVarArray(Claims);
        }
    }
}
