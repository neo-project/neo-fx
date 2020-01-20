using NeoFx.Storage;
using System.Buffers;
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

        public static bool TryRead(ref SpanReader<byte> reader, byte version, [NotNullWhen(true)] out RegisterTransaction? tx)
        {
            if (reader.TryRead(out byte assetType)
                && reader.TryReadVarString(1024, out var name)
                && Fixed8.TryRead(ref reader, out Fixed8 amount)
                && reader.TryRead(out byte precision)
                && EncodedPublicKey.TryRead(ref reader, out EncodedPublicKey owner)
                && UInt160.TryRead(ref reader, out UInt160 admin)
                && reader.TryReadVarArray<TransactionAttribute>(TransactionAttribute.TryRead, out var attributes)
                && reader.TryReadVarArray<CoinReference>(CoinReference.TryRead, out var inputs)
                && reader.TryReadVarArray<TransactionOutput>(TransactionOutput.TryRead, out var outputs)
                && reader.TryReadVarArray<Witness>(Witness.TryRead, out var witnesses))
            {
                tx = new RegisterTransaction((AssetType)assetType, name, amount, precision, owner, admin, version,
                                             attributes, inputs, outputs, witnesses);
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
