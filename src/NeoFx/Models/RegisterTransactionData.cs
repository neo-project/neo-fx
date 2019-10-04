using System;
using System.Buffers;
using System.Diagnostics;

namespace NeoFx.Models
{
    public readonly struct RegisterTransactionData
    {
        public readonly AssetType AssetType;
        public readonly string Name;
        public readonly /*Fixed8*/ long Amount;
        public readonly byte Precision;
        public readonly /*ECPoint*/ byte Owner;
        public readonly UInt160 Admin;

        public RegisterTransactionData(AssetType assetType, string name, long amount, byte precision, byte owner, UInt160 admin)
        {
            AssetType = assetType;
            Name = name;
            Amount = amount;
            Precision = precision;
            Owner = owner;
            Admin = admin;
        }

        public static bool TryRead(ReadOnlyMemory<byte> memory, out RegisterTransactionData data)
        {
            var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(memory));

            if (reader.TryRead(out var assetType)
                && reader.TryReadVarString(out var name)
                && reader.TryReadInt64LittleEndian(out var amount)
                && reader.TryRead(out var precision)
                && reader.TryRead(out var owner) && owner == 0
                && reader.TryReadUInt160(out var admin))
            {
                Debug.Assert(reader.Remaining == 0);
                data = new RegisterTransactionData((AssetType)assetType, name, amount, precision, owner, admin);
                return true;
            }

            data = default;
            return false;
        }
    }
}
