using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{
    public abstract class Transaction
    {
        public readonly byte Version;
        public readonly ReadOnlyMemory<TransactionAttribute> Attributes;
        public readonly ReadOnlyMemory<CoinReference> Inputs;
        public readonly ReadOnlyMemory<TransactionOutput> Outputs;
        public readonly ReadOnlyMemory<Witness> Witnesses;

        protected Transaction(byte version, ReadOnlyMemory<TransactionAttribute> attributes,
                              ReadOnlyMemory<CoinReference> inputs, ReadOnlyMemory<TransactionOutput> outputs,
                              ReadOnlyMemory<Witness> witnesses)
        {
            Version = version;
            Attributes = attributes;
            Inputs = inputs;
            Outputs = outputs;
            Witnesses = witnesses;
        }
    }
}
