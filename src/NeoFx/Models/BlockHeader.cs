using DevHawk.Buffers;
using NeoFx.Storage;
using System;

namespace NeoFx.Models
{
    public readonly struct BlockHeader
    {
        public readonly uint Version;
        public readonly UInt256 PreviousHash;
        public readonly UInt256 MerkleRoot;
        public readonly DateTimeOffset Timestamp;
        public readonly uint Index;
        public readonly ulong ConsensusData;
        public readonly UInt160 NextConsensus;
        public readonly Witness Witness;

        public BlockHeader(uint version, in UInt256 previousHash, in UInt256 merkleRoot, uint timestamp, uint index, ulong consensusData, in UInt160 nextConsensus, in Witness witness)
            : this(version, previousHash, merkleRoot, DateTimeOffset.FromUnixTimeSeconds(timestamp), index, consensusData, nextConsensus, witness)
        {
        }

        public BlockHeader(uint version, in UInt256 previousHash, in UInt256 merkleRoot, DateTimeOffset timestamp, uint index, ulong consensusData, in UInt160 nextConsensus, in Witness witness)
        {
            Version = version;
            PreviousHash = previousHash;
            MerkleRoot = merkleRoot;
            Timestamp = timestamp;
            Index = index;
            ConsensusData = consensusData;
            NextConsensus = nextConsensus;
            Witness = witness;
        }

        public static bool TryRead(ref BufferReader<byte> reader, out BlockHeader value)
        {
            if (reader.TryReadLittleEndian(out uint version)
                && UInt256.TryRead(ref reader, out var prevHash)
                && UInt256.TryRead(ref reader, out var merkleRoot)
                && reader.TryReadLittleEndian(out uint timestamp)
                && reader.TryReadLittleEndian(out uint index)
                && reader.TryReadLittleEndian(out ulong consensusData)
                && UInt160.TryRead(ref reader, out var nextConsensus)
                && reader.TryRead(out byte witnessCount) && witnessCount == 1
                && Witness.TryRead(ref reader, out Witness witness))
            {
                value = new BlockHeader(version, prevHash, merkleRoot, timestamp, index, consensusData, nextConsensus, witness);
                return true;
            }

            value = default;
            return false;
        }
    }
}
