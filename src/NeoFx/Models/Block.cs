using DevHawk.Buffers;
using NeoFx.Storage;
using System;
using System.Collections.Immutable;

namespace NeoFx.Models
{
    public readonly struct Block : IWritable<Block>
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

        public int Size => Header.Size + Transactions.GetVarSize(tx => tx.Size);

        public Block(in BlockHeader header, in ImmutableArray<Transaction> transactions)
        {
            Header = header;
            Transactions = transactions;
        }

        public static bool TryRead(ref BufferReader<byte> reader, out Block value)
        {
            if (BlockHeader.TryRead(ref reader, out var header)
                && reader.TryReadVarArray<Transaction, Transaction.Factory>(out var transactions))
            {
                value = new Block(header, transactions);
                return true;
            }

            value = default;
            return false;
        }

        public void WriteTo(ref BufferWriter<byte> writer)
        {
            Header.WriteTo(ref writer);
            writer.WriteVarArray(Transactions);
        }
    }
}
