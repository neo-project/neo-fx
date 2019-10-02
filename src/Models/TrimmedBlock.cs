using System;
using System.Buffers;

namespace NeoFx.Models
{
    public readonly struct TrimmedBlock
    {
        public readonly BlockHeader Header;
        public readonly ReadOnlyMemory<UInt256> Hashes;

        public TrimmedBlock(BlockHeader header, ReadOnlyMemory<UInt256> hashes)
        {
            Header = header;
            Hashes = hashes;
        }

        public static bool TryRead(ref SequenceReader<byte> reader, out TrimmedBlock value)
        {
            if (BlockHeader.TryRead(ref reader, out var header)
                && reader.TryReadVarArray<UInt256>(UInt256.TryRead, out var hashes))
            {
                value = new TrimmedBlock(header, hashes);
                return true;
            }

            value = default;
            return false;
        }
    }
}
