using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace NeoFx.Models
{
    public abstract class Transaction
    {
        public readonly byte Version;
        public readonly ImmutableArray<TransactionAttribute> Attributes;
        public readonly ImmutableArray<CoinReference> Inputs;
        public readonly ImmutableArray<TransactionOutput> Outputs;
        public readonly ImmutableArray<Witness> Witnesses;

        protected Transaction(byte version,
                              ImmutableArray<TransactionAttribute> attributes,
                              ImmutableArray<CoinReference> inputs,
                              ImmutableArray<TransactionOutput> outputs,
                              ImmutableArray<Witness> witnesses)
        {
            Version = version;
            Attributes = attributes;
            Inputs = inputs;
            Outputs = outputs;
            Witnesses = witnesses;
        }
    }
}
