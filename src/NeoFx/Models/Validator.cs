using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{
    public readonly struct Validator
    {
        public readonly EncodedPublicKey PublicKey;
        public readonly bool Registered;
        public readonly Fixed8 Votes;

        public Validator(EncodedPublicKey publicKey, bool registered, Fixed8 votes)
        {
            PublicKey = publicKey;
            Registered = registered;
            Votes = votes;
        }
    }
}
