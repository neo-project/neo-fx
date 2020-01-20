using System.Collections.Immutable;

namespace NeoFx.Models
{
    public sealed class ClaimTransaction : Transaction
    {
        public readonly ImmutableArray<CoinReference> Claims;

        public ClaimTransaction(ImmutableArray<CoinReference> claims,
                                byte version,
                                ImmutableArray<TransactionAttribute> attributes,
                                ImmutableArray<CoinReference> inputs,
                                ImmutableArray<TransactionOutput> outputs,
                                ImmutableArray<Witness> witnesses)
            : base(version, attributes, inputs, outputs, witnesses)
        {
            Claims = claims;
        }
    }
}
