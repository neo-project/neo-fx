using System.Collections.Immutable;

namespace NeoFx.Models
{
    public readonly struct Account
    {
        public readonly UInt160 ScriptHash;
        public readonly bool IsFrozen;
        public readonly ImmutableArray<EncodedPublicKey> Votes;
        public readonly ImmutableDictionary<UInt256, Fixed8> Balances;

        public Account(UInt160 scriptHash, bool isFrozen, ImmutableArray<EncodedPublicKey> votes, ImmutableDictionary<UInt256, Fixed8> balances)
        {
            ScriptHash = scriptHash;
            IsFrozen = isFrozen;
            Votes = votes;
            Balances = balances;
        }

        public Account(UInt160 scriptHash)
            : this(scriptHash, false, default, ImmutableDictionary<UInt256, Fixed8>.Empty)
        {
        }
    }
}
