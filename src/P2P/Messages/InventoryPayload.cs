using DevHawk.Buffers;
using NeoFx.Storage;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace NeoFx.P2P.Messages
{
    // Used for inv and getdata messages
    public readonly struct InventoryPayload : IWritable<InventoryPayload>
    {
        public enum InventoryType : byte
        {
            Transaction = 0x01,
            Block = 0x02,
            Consensus = 0xe0
        }

        public readonly InventoryType Type;
        public readonly ImmutableArray<UInt256> Hashes;

        public int Size => 1 + Hashes.GetVarSize(UInt256.Size);

        public InventoryPayload(InventoryType type, ImmutableArray<UInt256> hashes)
        {
            Type = type;
            Hashes = hashes;
        }

        public InventoryPayload(InventoryType type, IEnumerable<UInt256> hashes)
            : this(type, hashes.ToImmutableArray())
        {
        }
        
        public static bool TryRead(ref BufferReader<byte> reader, out InventoryPayload payload)
        {
            if (reader.TryRead(out byte type)
                && reader.TryReadVarArray<UInt256, UInt256.Factory>(out var hashes))
            {
                payload = new InventoryPayload((InventoryType)type, hashes);
                return true;
            }

            payload = default;
            return false;
        }

        public void WriteTo(ref BufferWriter<byte> writer)
        {
            writer.Write((byte)Type);
            writer.WriteVarArray(Hashes);
        }
    }
}
