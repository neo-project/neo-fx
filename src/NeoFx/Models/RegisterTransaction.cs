using NeoFx.Storage;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

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
                                   IEnumerable<TransactionAttribute> attributes,
                                   IEnumerable<CoinReference> inputs,
                                   IEnumerable<TransactionOutput> outputs,
                                   IEnumerable<Witness> witnesses)
            : base(version, attributes, inputs, outputs, witnesses)
        {
            AssetType = assetType;
            Name = name;
            Amount = amount;
            Precision = precision;
            Owner = owner;
            Admin = admin;
        }

        private RegisterTransaction(AssetType assetType,
                           string name,
                           Fixed8 amount,
                           byte precision,
                           EncodedPublicKey owner,
                           in UInt160 admin,
                           byte version,
                           CommonData commonData)
            : base(version, commonData)
        {
            AssetType = assetType;
            Name = name;
            Amount = amount;
            Precision = precision;
            Owner = owner;
            Admin = admin;
        }

        public static bool TryRead(ref BufferReader<byte> reader, byte version, [NotNullWhen(true)] out RegisterTransaction? tx)
        {
            if (reader.TryRead(out byte assetType)
                && reader.TryReadVarString(1024, out var name)
                && Fixed8.TryRead(ref reader, out Fixed8 amount)
                && reader.TryRead(out byte precision)
                && EncodedPublicKey.TryRead(ref reader, out EncodedPublicKey owner)
                && UInt160.TryRead(ref reader, out UInt160 admin)
                && TryReadCommonData(ref reader, out var commonData))
            {
                tx = new RegisterTransaction((AssetType)assetType, name, amount, precision, owner, admin, version, commonData);
                return true;
            }

            tx = null;
            return false;
        }

        public override void WriteTransactionData(IBufferWriter<byte> writer)
        {
            writer.WriteLittleEndian((byte)TransactionType.Register);
            writer.WriteLittleEndian(Version);
            writer.WriteLittleEndian((byte)AssetType);
            Debug.Assert(Name.Length <= 1024);
            writer.WriteVarString(Name);
            writer.WriteLittleEndian(Amount);
            writer.WriteLittleEndian(Precision);
            writer.Write(Owner);
            writer.WriteLittleEndian(Admin);
        }
    }
}
