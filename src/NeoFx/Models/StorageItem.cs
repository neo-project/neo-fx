using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace NeoFx.Models
{
    public readonly struct StorageItem
    {
        public readonly ImmutableArray<byte> Value;
        public readonly bool IsConstant;

        public StorageItem(ImmutableArray<byte> value, bool isConstant)
        {
            Value = value;
            IsConstant = isConstant;
        }
    }
}
