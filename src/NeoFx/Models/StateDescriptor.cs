using DevHawk.Buffers;
using NeoFx.Storage;
using System.Buffers;
using System.Collections.Immutable;

namespace NeoFx.Models
{
    public readonly struct StateDescriptor : IWritable<StateDescriptor>
    {
        public readonly struct Factory : IFactoryReader<StateDescriptor>
        {
            public bool TryReadItem(ref BufferReader<byte> reader, out StateDescriptor value) => StateDescriptor.TryRead(ref reader, out value);
        }

        public enum StateType : byte
        {
            Account = 0x40,
            Validator = 0x48
        }

        public readonly StateType Type;
        public readonly ImmutableArray<byte> Key;
        public readonly string Field;
        public readonly ImmutableArray<byte> Value;

        public int Size => sizeof(StateType) + Key.GetVarSize() + Field.GetVarSize() + Value.GetVarSize();

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

        public void WriteTo(ref BufferWriter<byte> writer)
        {
            writer.Write((byte)Type);
            writer.WriteVarArray(Key);
            writer.WriteVarString(Field);
            writer.WriteVarArray(Value);
        }
    }
}
