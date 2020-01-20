using NeoFx.Storage;
using System.Collections.Immutable;

namespace NeoFx.Models
{
    public readonly struct StateDescriptor
    {
        public enum StateType : byte
        {
            Account = 0x40,
            Validator = 0x48
        }

        public readonly StateType Type;
        public readonly ImmutableArray<byte> Key;
        public readonly string Field;
        public readonly ImmutableArray<byte> Value;

        public StateDescriptor(StateType type, ImmutableArray<byte> key, string field, ImmutableArray<byte> value)
        {
            Type = type;
            Key = key;
            Field = field;
            Value = value;
        }

        public static bool TryRead(ref SpanReader<byte> reader, out StateDescriptor descriptor)
        {
            if (reader.TryRead(out var type)
                && reader.TryReadVarArray(100, out var key)
                && reader.TryReadVarString(32, out var field)
                && reader.TryReadVarArray(65535, out var value))
            {
                descriptor = new StateDescriptor((StateType)type, key, field, value);
                return true;
            }

            descriptor = default;
            return false;
        }

    }
}
