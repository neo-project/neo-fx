using System;
using System.Buffers;
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
    }
}
