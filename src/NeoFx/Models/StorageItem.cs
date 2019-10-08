using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
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
    }
}
