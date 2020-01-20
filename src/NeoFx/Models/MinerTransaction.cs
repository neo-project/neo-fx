using NeoFx.Storage;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NeoFx.Models
{
    public sealed class MinerTransaction : Transaction
    {
        public readonly uint Nonce;

        public MinerTransaction(uint nonce,
                                byte version,
                                ImmutableArray<TransactionAttribute> attributes,
                                ImmutableArray<CoinReference> inputs,
                                ImmutableArray<TransactionOutput> outputs,
                                ImmutableArray<Witness> witnesses)
            : base(version, attributes, inputs, outputs, witnesses)
        {
            Nonce = nonce;
        }

        public static bool TryRead(ref SpanReader<byte> reader, byte version, [NotNullWhen(true)] out MinerTransaction? tx)
        {
            if (reader.TryRead(out uint nonce)
                && reader.TryReadVarArray<TransactionAttribute>(TransactionAttribute.TryRead, out var attributes)
                && reader.TryReadVarArray<CoinReference>(CoinReference.TryRead, out var inputs)
                && reader.TryReadVarArray<TransactionOutput>(TransactionOutput.TryRead, out var outputs)
                && reader.TryReadVarArray<Witness>(Witness.TryRead, out var witnesses))
            {
                tx = new MinerTransaction(nonce, version, attributes, inputs, outputs, witnesses);
                return true;
            }

            tx = null;
            return false;
        }

    }
}
