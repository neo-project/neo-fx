using System.Collections.Immutable;

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
