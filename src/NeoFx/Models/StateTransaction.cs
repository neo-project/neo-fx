using NeoFx.Storage;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NeoFx.Models
{
    public sealed class StateTransaction : Transaction
    {
        public readonly ImmutableArray<StateDescriptor> Descriptors;

        public StateTransaction(ImmutableArray<StateDescriptor> descriptors,
                                byte version,
                                ImmutableArray<TransactionAttribute> attributes,
                                ImmutableArray<CoinReference> inputs,
                                ImmutableArray<TransactionOutput> outputs,
                                ImmutableArray<Witness> witnesses)
            : base(version, attributes, inputs, outputs, witnesses)
        {
            Descriptors = descriptors;
        }

        public static bool TryRead(ref SpanReader<byte> reader, byte version, [NotNullWhen(true)] out StateTransaction? tx)
        {
            if (reader.TryReadVarArray<StateDescriptor>(StateDescriptor.TryRead, out var descriptors)
                && reader.TryReadVarArray<TransactionAttribute>(TransactionAttribute.TryRead, out var attributes)
                && reader.TryReadVarArray<CoinReference>(CoinReference.TryRead, out var inputs)
                && reader.TryReadVarArray<TransactionOutput>(TransactionOutput.TryRead, out var outputs)
                && reader.TryReadVarArray<Witness>(Witness.TryRead, out var witnesses))
            {
                tx = new StateTransaction(descriptors, version, attributes, inputs, outputs, witnesses);
                return true;
            }

            tx = null;
            return false;
        }

    }
}
