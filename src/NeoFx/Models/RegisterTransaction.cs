using System.Collections.Immutable;

namespace NeoFx.Models
{
    public sealed class RegisterTransaction : Transaction
    {
        public readonly AssetType AssetType;
        public readonly string Name;
        public readonly Fixed8 Amount;
        public readonly byte Precision;
        public readonly EncodedPublicKey Owner;
        public readonly UInt160 Admin;

        public RegisterTransaction(AssetType assetType,
                                   string name,
                                   Fixed8 amount,
                                   byte precision,
                                   EncodedPublicKey owner,
                                   in UInt160 admin,
                                   byte version,
                                   ImmutableArray<TransactionAttribute> attributes,
                                   ImmutableArray<CoinReference> inputs,
                                   ImmutableArray<TransactionOutput> outputs,
                                   ImmutableArray<Witness> witnesses)
            : base(version, attributes, inputs, outputs, witnesses)
        {
            AssetType = assetType;
            Name = name;
            Amount = amount;
            Precision = precision;
            Owner = owner;
            Admin = admin;
        }
    }
}
