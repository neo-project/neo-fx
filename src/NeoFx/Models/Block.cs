using System;
using System.Collections.Immutable;

namespace NeoFx.Models
{
    public readonly struct Block
    {
        public readonly BlockHeader Header;
        public readonly ImmutableArray<Transaction> Transactions;

        public readonly uint Version => Header.Version;
        public readonly UInt256 PreviousHash => Header.PreviousHash;
        public readonly UInt256 MerkleRoot => Header.MerkleRoot;
        public readonly DateTimeOffset Timestamp => Header.Timestamp;
        public readonly uint Index => Header.Index;
        public readonly ulong ConsensusData => Header.ConsensusData;
        public readonly UInt160 NextConsensus => Header.NextConsensus;
        public readonly Witness Witness => Header.Witness;

        public Block(in BlockHeader header, in ImmutableArray<Transaction> transactions)
        {
            Header = header;
            Transactions = transactions;
        }
    }
}
