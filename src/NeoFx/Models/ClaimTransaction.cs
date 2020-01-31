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
                                IEnumerable<TransactionAttribute>? attributes = null,
                                IEnumerable<CoinReference>? inputs = null,
                                IEnumerable<TransactionOutput>? outputs = null,
                                IEnumerable<Witness>? witnesses = null)
            : base(version, attributes, inputs, outputs, witnesses)
        {
            Claims = claims == default ? ImmutableArray.Create<CoinReference>() : claims;
        }

        private ClaimTransaction(ImmutableArray<CoinReference> claims,
                                 byte version,
                                 in CommonData commonData)
            : base(version, commonData)
        {
            Claims = claims == default ? ImmutableArray.Create<CoinReference>() : claims;
        }

        public static bool TryRead(ref BufferReader<byte> reader, byte version, [NotNullWhen(true)] out ClaimTransaction? tx)
        {
            if (reader.TryReadVarArray<CoinReference, CoinReference.Factory>(out var claims)
                && TryReadCommonData(ref reader, out var commonData))
            {
                tx = new ClaimTransaction(claims, version, commonData);
                return true;
            }

            tx = null;
            return false;
        }

        public override TransactionType GetTransactionType()
            => TransactionType.Claim;

        public override int GetTransactionDataSize()
            => Claims.GetVarSize(CoinReference.Size);

        public override void WriteTransactionData(ref BufferWriter<byte> writer)
        {
            writer.WriteVarArray(Claims);
        }
    }
}
