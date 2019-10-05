using System;
using System.Buffers;

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
    }
}
