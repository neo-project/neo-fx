using NeoFx.Storage;
using System.Buffers;

namespace NeoFx.Models
{
    public readonly struct TransactionOutput : IWritable<TransactionOutput>
    {
        public readonly UInt256 AssetId;
        public readonly Fixed8 Value;
        public readonly UInt160 ScriptHash;

        public TransactionOutput(in UInt256 assetId, Fixed8 value, in UInt160 scriptHash)
        {
            AssetId = assetId;
            Value = value;
            ScriptHash = scriptHash;
        }

        public static bool TryRead(ref SpanReader<byte> reader, out TransactionOutput value)
        {
            if (reader.TryRead(out UInt256 assetId)
               && Fixed8.TryRead(ref reader, out var outputValue)
               && UInt160.TryRead(ref reader, out var scriptHash))
            {
                value = new TransactionOutput(assetId, outputValue, scriptHash);
                return true;
            }

            value = default;
            return false;
        }

        public void Write(IBufferWriter<byte> writer)
        {
            writer.Write(AssetId);
            writer.Write(Value);
            writer.Write(ScriptHash);
        }
    }
}
