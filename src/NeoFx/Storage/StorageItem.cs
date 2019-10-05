using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Storage
{
    public readonly struct StorageItem
    {
        public readonly ReadOnlyMemory<byte> Value;
        public readonly bool IsConstant;

        public StorageItem(ReadOnlyMemory<byte> value, bool isConstant)
        {
            Value = value;
            IsConstant = isConstant;
        }

        public static bool TryRead(ref SequenceReader<byte> reader, out StorageItem value)
        {
            if (reader.TryReadVarByteArray(out var _value)
                && reader.TryRead(out var isConstant))
            {
                value = new StorageItem(_value, isConstant != 0);
                return true;
            }

            value = default;
            return false;
        }
    }
}
