using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{

    public class BlockHeader
    {
        public uint Version;
        public UInt256 PrevHash;
        public UInt256 MerkleRoot;
        public uint Timestamp;
        public uint Index;
        public ulong ConsensusData;
        public UInt160 NextConsensus;
        public Witness Witness = new Witness();
    }

    public class Block
    {
        public BlockHeader Header = new BlockHeader();
        public Transaction[] Transactions = Array.Empty<Transaction>();
    }
}
