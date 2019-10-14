using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{
    public sealed class ContractTransaction : Transaction
    {
        public ContractTransaction(byte version, ReadOnlyMemory<TransactionAttribute> attributes, ReadOnlyMemory<CoinReference> inputs, ReadOnlyMemory<TransactionOutput> outputs, ReadOnlyMemory<Witness> witnesses)
            : base(version, attributes, inputs, outputs, witnesses)
        {
        }
    }
}
