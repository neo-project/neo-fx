using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{
    [Obsolete]
    public sealed class EnrollmentTransaction : Transaction
    {
        public readonly EncodedPublicKey PublicKey;

        public EnrollmentTransaction(EncodedPublicKey publicKey, byte version,
                                        ReadOnlyMemory<TransactionAttribute> attributes,
                                        ReadOnlyMemory<CoinReference> inputs, ReadOnlyMemory<TransactionOutput> outputs,
                                        ReadOnlyMemory<Witness> witnesses) 
            : base(version, attributes, inputs, outputs, witnesses)
        {
            PublicKey = publicKey;
        }
    }
}
