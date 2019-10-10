using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace NeoFx.Models
{
    public readonly struct Account
    {
        public readonly UInt160 ScriptHash;
        public readonly bool IsFrozen;
        public readonly ReadOnlyMemory<EncodedPublicKey> Votes;
        public readonly ImmutableDictionary<UInt256, Fixed8> Balances;

        public Account(UInt160 scriptHash, bool isFrozen, ReadOnlyMemory<EncodedPublicKey> votes, ImmutableDictionary<UInt256, Fixed8> balances)
        {
            ScriptHash = scriptHash;
            IsFrozen = isFrozen;
            Votes = votes;
            Balances = balances;
        }
    }
}
