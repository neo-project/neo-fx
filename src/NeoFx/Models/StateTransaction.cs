using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{
    public sealed class StateTransaction : Transaction
    {
        public readonly ReadOnlyMemory<StateDescriptor> Descriptors;

        public StateTransaction(ReadOnlyMemory<StateDescriptor> descriptors, byte version,
                                ReadOnlyMemory<TransactionAttribute> attributes, ReadOnlyMemory<CoinReference> inputs,
                                ReadOnlyMemory<TransactionOutput> outputs, ReadOnlyMemory<Witness> witnesses)
            : base(version, attributes, inputs, outputs, witnesses)
        {
            Descriptors = descriptors;
        }
    }
}
