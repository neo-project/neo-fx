using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace NeoFx.Models
{
    public sealed class InvocationTransaction : Transaction
    {
        public readonly ImmutableArray<byte> Script;
        public readonly Fixed8 Gas;

        public InvocationTransaction(ImmutableArray<byte> script,
                                     Fixed8 gas,
                                     byte version,
                                     ImmutableArray<TransactionAttribute> attributes,
                                     ImmutableArray<CoinReference> inputs,
                                     ImmutableArray<TransactionOutput> outputs,
                                     ImmutableArray<Witness> witnesses)
            : base(version, attributes, inputs, outputs, witnesses)
        {
            Script = script;
            Gas = gas;
        }
    }
}
