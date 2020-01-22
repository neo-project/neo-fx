﻿using DevHawk.Buffers;
using NeoFx.Storage;
using System.Buffers;
using System.Collections.Immutable;

namespace NeoFx.Models
{
    public readonly struct StateDescriptor : IFactoryReader<StateDescriptor>, IWritable<StateDescriptor>
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

        public static bool TryRead(ref BufferReader<byte> reader, out StateDescriptor descriptor)
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

        bool IFactoryReader<StateDescriptor>.TryReadItem(ref BufferReader<byte> reader, out StateDescriptor value) => TryRead(ref reader, out value);

        public void Write(ref BufferWriter<byte> writer)
        {
            writer.WriteLittleEndian((byte)Type);
            writer.WriteVarArray(Key);
            writer.WriteVarString(Field);
            writer.WriteVarArray(Value);
        }
    }
}
