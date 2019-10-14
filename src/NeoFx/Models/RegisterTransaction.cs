using System;
using System.Collections.Generic;
using System.Text;

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

        public RegisterTransaction(AssetType assetType, string name, Fixed8 amount, byte precision,
                                   EncodedPublicKey owner, in UInt160 admin, byte version,
                                   ReadOnlyMemory<TransactionAttribute> attributes, ReadOnlyMemory<CoinReference> inputs,
                                   ReadOnlyMemory<TransactionOutput> outputs, ReadOnlyMemory<Witness> witnesses)
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
