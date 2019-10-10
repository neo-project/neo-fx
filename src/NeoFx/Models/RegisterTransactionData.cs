using System;
using System.Buffers;
using System.Diagnostics;

namespace NeoFx.Models
{
    public readonly struct RegisterTransactionData
    {
        public readonly AssetType AssetType;
        public readonly string Name;
        public readonly Fixed8 Amount;
        public readonly byte Precision;
        public readonly /*ECPoint*/ byte Owner;
        public readonly UInt160 Admin;

        public RegisterTransactionData(AssetType assetType, string name, Fixed8 amount, byte precision, byte owner, UInt160 admin)
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
