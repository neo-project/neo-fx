using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{
    public sealed class ClaimTransaction : Transaction
    {
        public readonly ReadOnlyMemory<CoinReference> Claims;

        public ClaimTransaction(ReadOnlyMemory<CoinReference> claims, byte version,
                                ReadOnlyMemory<TransactionAttribute> attributes, ReadOnlyMemory<CoinReference> inputs,
                                ReadOnlyMemory<TransactionOutput> outputs, ReadOnlyMemory<Witness> witnesses) 
            : base(version, attributes, inputs, outputs, witnesses)
        {
            Claims = claims;
        }
    }
}
