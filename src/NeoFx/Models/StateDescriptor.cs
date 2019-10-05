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
    }
}
