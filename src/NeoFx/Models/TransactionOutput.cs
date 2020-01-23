using DevHawk.Buffers;
using NeoFx.Storage;
using System.Buffers;

namespace NeoFx.Models
{
    public readonly struct TransactionOutput : IWritable<TransactionOutput>
    {
        public readonly struct Factory : IFactoryReader<TransactionOutput>
        {
            public bool TryReadItem(ref BufferReader<byte> reader, out TransactionOutput value) => TransactionOutput.TryRead(ref reader, out value);
        }

        public readonly UInt256 AssetId;
        public readonly Fixed8 Value;
        public readonly UInt160 ScriptHash;

        public const int Size = Fixed8.Size + UInt256.Size + UInt160.Size;

        public TransactionOutput(in UInt256 assetId, Fixed8 value, in UInt160 scriptHash)
        {
            AssetId = assetId;
            Value = value;
            ScriptHash = scriptHash;
        }

        public static bool TryRead(ref BufferReader<byte> reader, out TransactionOutput value)
        {
            if (UInt256.TryRead(ref reader, out var assetId)
               && Fixed8.TryRead(ref reader, out var outputValue)
               && UInt160.TryRead(ref reader, out var scriptHash))
            {
                value = new TransactionOutput(assetId, outputValue, scriptHash);
                return true;
            }

            value = default;
            return false;
        }

        public void WriteTo(ref BufferWriter<byte> writer)
        {
            AssetId.WriteTo(ref writer);
            Value.WriteTo(ref writer);
            ScriptHash.WriteTo(ref writer);
        }
    }
}
