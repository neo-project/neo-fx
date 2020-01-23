using DevHawk.Buffers;
using NeoFx.Storage;

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

        public static bool TryRead(ref BufferReader<byte> reader, out Asset value)
        {
            if (UInt256.TryRead(ref reader, out var assetId)
                && reader.TryRead(out byte assetType)
                && reader.TryReadVarString(out var name)
                && Fixed8.TryRead(ref reader, out Fixed8 amount)
                && Fixed8.TryRead(ref reader, out Fixed8 available)
                && reader.TryRead(out byte precision)
                && reader.TryRead(out byte _) // feeMode
                && Fixed8.TryRead(ref reader, out Fixed8 fee)
                && UInt160.TryRead(ref reader, out UInt160 feeAddress)
                && EncodedPublicKey.TryRead(ref reader, out EncodedPublicKey owner)
                && UInt160.TryRead(ref reader, out UInt160 admin)
                && UInt160.TryRead(ref reader, out UInt160 issuer)
                && reader.TryReadLittleEndian(out uint expiration)
                && reader.TryRead(out byte isFrozen))
            {
                value = new Asset(
                    assetId: assetId,
                    assetType: (AssetType)assetType,
                    name: name,
                    amount: amount,
                    available: available,
                    precision: precision,
                    fee: fee,
                    feeAddress: feeAddress,
                    owner: owner,
                    admin: admin,
                    issuer: issuer,
                    expiration: expiration,
                    isFrozen: isFrozen != 0);
                return true;
            }

            value = default;
            return false;
        }
    }
}
