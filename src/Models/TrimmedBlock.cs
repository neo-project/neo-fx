using System;
using System.Buffers;

namespace NeoFx.Models
{
    public readonly struct TrimmedBlock
    {
        public readonly BlockHeader Header;
        public readonly ReadOnlyMemory<UInt256> Hashes;

        public readonly uint Version => Header.Version;
        public readonly UInt256 PreviousHash => Header.PreviousHash;
        public readonly UInt256 MerkleRoot => Header.MerkleRoot;
        public readonly DateTimeOffset Timestamp => Header.Timestamp;
        public readonly uint Index => Header.Index;
        public readonly ulong ConsensusData => Header.ConsensusData;
        public readonly UInt160 NextConsensus => Header.NextConsensus;
        public readonly Witness Witness => Header.Witness;

        public TrimmedBlock(in BlockHeader header, ReadOnlyMemory<UInt256> hashes)
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
