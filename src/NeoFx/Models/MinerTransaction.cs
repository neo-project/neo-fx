using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

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
    }
}
