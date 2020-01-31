using DevHawk.Buffers;
using NeoFx.Models;
using System.Diagnostics.CodeAnalysis;

namespace NeoFx.P2P.Messages
{
    public readonly struct BlockPayload : IPayload<BlockPayload>
    {
        public readonly Block Block;

        public BlockPayload(in Block block)
        {
            Block = block;
        }

        public int Size => Block.Size;

        public static bool TryRead(ref BufferReader<byte> reader, out BlockPayload payload)
        {
            if (Block.TryRead(ref reader, out var block))
            {
                payload = new BlockPayload(block);
                return true;
            }

            payload = default!;
            return false;
        }

        public void WriteTo(ref BufferWriter<byte> writer)
        {
            Block.WriteTo(ref writer);
        }

    }
}
