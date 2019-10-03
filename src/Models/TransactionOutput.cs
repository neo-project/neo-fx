using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{
    public readonly struct TransactionOutput
    {
        public readonly UInt256 AssetId;
        public readonly /*Fixed8*/ long Value;
        public readonly UInt160 ScriptHash;

        public const int Size = sizeof(long) + UInt256.Size + UInt160.Size;

        public TransactionOutput(UInt256 assetId, /*Fixed8 */long value, UInt160 scriptHash)
        {
            AssetId = assetId;
            Value = value;
            ScriptHash = scriptHash;
        }

        public static bool TryRead(ref SequenceReader<byte> reader, out TransactionOutput value)
        {
            if (UInt256.TryRead(ref reader, out var assetId)
               && reader.TryReadInt64LittleEndian(out long outputValue)
               && UInt160.TryRead(ref reader, out var scriptHash))
            {
                value = new TransactionOutput(assetId, outputValue, scriptHash);
                return true;
            }

            value = default;
            return false;
        }
    }
}
