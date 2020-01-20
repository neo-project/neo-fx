using System.Collections.Immutable;

namespace NeoFx.Models
{
    public sealed class IssueTransaction : Transaction
    {
        public IssueTransaction(byte version,
                                ImmutableArray<TransactionAttribute> attributes,
                                ImmutableArray<CoinReference> inputs,
                                ImmutableArray<TransactionOutput> outputs,
                                ImmutableArray<Witness> witnesses)
            : base(version, attributes, inputs, outputs, witnesses)
        {
        }
    }
}
