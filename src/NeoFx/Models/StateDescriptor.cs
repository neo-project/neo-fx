using System;
using System.Buffers;

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
        public readonly ReadOnlyMemory<byte> Key;
        public readonly string Field;
        public readonly ReadOnlyMemory<byte> Value;

        public readonly int Size => 1 + Key.GetVarSize() + Field.GetVarSize() + Value.GetVarSize();

        public StateDescriptor(StateType type, ReadOnlyMemory<byte> key, string field, ReadOnlyMemory<byte> value)
        {
            Type = type;
            Key = key;
            Field = field;
            Value = value;
        }

        public static bool TryRead(ref SequenceReader<byte> reader, out StateDescriptor descriptor)
        {
            if (reader.TryRead(out var type)
                && reader.TryReadVarArray(out var key)
                && reader.TryReadVarString(out var field)
                && reader.TryReadVarArray(out var value))
            {
                descriptor = new StateDescriptor((StateType)type, key, field, value);
                return true;
            }

            descriptor = default;
            return false;
        }
    }
}
