using System.Collections.Immutable;

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
    }
}
