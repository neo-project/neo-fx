using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{
    public readonly struct Asset
    {
        public readonly UInt256 AssetId;
        public readonly AssetType AssetType;
        public readonly string Name;
        public readonly Fixed8 Amount;
        public readonly Fixed8 Available;
        public readonly byte Precision;
        public readonly Fixed8 Fee;
        public readonly UInt160 FeeAddress;
        public readonly EncodedPublicKey Owner;
        public readonly UInt160 Admin;
        public readonly UInt160 Issuer;
        public readonly uint Expiration; // should this be a Timestamp?
        public readonly bool IsFrozen;

        public Asset(UInt256 assetId, AssetType assetType, string name, Fixed8 amount, Fixed8 available, byte precision, Fixed8 fee, UInt160 feeAddress, EncodedPublicKey owner, UInt160 admin, UInt160 issuer, uint expiration, bool isFrozen)
        {
            AssetId = assetId;
            AssetType = assetType;
            Name = name;
            Amount = amount;
            Available = available;
            Precision = precision;
            Fee = fee;
            FeeAddress = feeAddress;
            Owner = owner;
            Admin = admin;
            Issuer = issuer;
            Expiration = expiration;
            IsFrozen = isFrozen;
        }
    }
}
