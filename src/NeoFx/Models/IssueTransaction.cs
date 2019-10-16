using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{
    public sealed class IssueTransaction : Transaction
    {
        public IssueTransaction(byte version, ReadOnlyMemory<TransactionAttribute> attributes, ReadOnlyMemory<CoinReference> inputs, ReadOnlyMemory<TransactionOutput> outputs, ReadOnlyMemory<Witness> witnesses)
            : base(version, attributes, inputs, outputs, witnesses)
        {
        }
    }
}
