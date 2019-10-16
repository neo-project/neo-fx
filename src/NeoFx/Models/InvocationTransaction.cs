using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{
    public sealed class InvocationTransaction : Transaction
    {
        public readonly ReadOnlyMemory<byte> Script;
        public readonly Fixed8 Gas;

        public InvocationTransaction(ReadOnlyMemory<byte> script, Fixed8 gas, byte version,
                                     ReadOnlyMemory<TransactionAttribute> attributes,
                                     ReadOnlyMemory<CoinReference> inputs, ReadOnlyMemory<TransactionOutput> outputs,
                                     ReadOnlyMemory<Witness> witnesses) 
            : base(version, attributes, inputs, outputs, witnesses)
        {
            Script = script;
            Gas = gas;
        }
    }
}
