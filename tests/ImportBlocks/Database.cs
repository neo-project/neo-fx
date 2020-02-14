using DevHawk.Buffers;
using NeoFx;
using NeoFx.Models;
using NeoFx.Storage;
using RocksDbSharp;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Diagnostics;

namespace ImportBlocks
{
    class Database : IDisposable
    {
        const string BLOCKS_FAMILY = "data:blocks";
        const string TRANSACTIONS_FAMILY = "data:transactions";
        const string BLOCK_INDEX_FAMILY = "ix:block-index";

        private readonly RocksDb db;

        public readonly ColumnFamilyHandle BlocksFamily;
        public readonly ColumnFamilyHandle TransactionsFamily;
        public readonly ColumnFamilyHandle BlockIndexFamily;

        public Database(string path)
        {
            var columnFamilies = new ColumnFamilies {
                { BLOCKS_FAMILY, new ColumnFamilyOptions() },
                { TRANSACTIONS_FAMILY, new ColumnFamilyOptions() },
                { BLOCK_INDEX_FAMILY, new ColumnFamilyOptions() }};

            var options = new DbOptions()
                .SetCreateIfMissing(true)
                .SetCreateMissingColumnFamilies(true);

            db = RocksDb.Open(options, path, columnFamilies);
            BlocksFamily = db.GetColumnFamily(BLOCKS_FAMILY);
            TransactionsFamily = db.GetColumnFamily(TRANSACTIONS_FAMILY);
            BlockIndexFamily = db.GetColumnFamily(BLOCK_INDEX_FAMILY);
        }
        public void Dispose()
        {
            db.Dispose();
        }

        void PutTransaction(in UInt256 hash, in Transaction tx, WriteBatch batch)
        {
            Span<byte> keyBuffer = stackalloc byte[UInt256.Size];
            hash.Write(keyBuffer);

            var txSize = tx.Size;
            using var owner = MemoryPool<byte>.Shared.Rent(txSize);
            var txSpan = owner.Memory.Span.Slice(0, txSize);
            var writer = new BufferWriter<byte>(txSpan);
            tx.WriteTo(ref writer);
            writer.Commit();
            Debug.Assert(writer.Span.IsEmpty);

            batch.Put(keyBuffer, txSpan, TransactionsFamily);
        }

        void PutBlock(in BlockHeader header, ReadOnlySpan<UInt256> txHashes, WriteBatch batch)
        {
            Span<byte> indexBuffer = stackalloc byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32BigEndian(indexBuffer, header.Index);

            Span<byte> hashBuffer = stackalloc byte[UInt256.Size];
            header.CalculateHash().Write(hashBuffer);

            var blockSize = header.Size + txHashes.GetVarSize(UInt256.Size);
            using var owner = MemoryPool<byte>.Shared.Rent(blockSize);
            var blockSpan = owner.Memory.Span.Slice(0, blockSize);
            var writer = new BufferWriter<byte>(blockSpan);
            header.WriteTo(ref writer);
            writer.WriteVarArray(txHashes);
            writer.Commit();
            Debug.Assert(writer.Span.IsEmpty);

            batch.Put(hashBuffer, blockSpan, BlocksFamily);
            batch.Put(hashBuffer, hashBuffer, BlockIndexFamily);
        }

        bool BlockExists(uint index)
        {
            Span<byte> indexBuffer = stackalloc byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32BigEndian(indexBuffer, index);
            return db.KeyExists(indexBuffer, BlockIndexFamily);
        }

        public void AddBlock(in Block block)
        {
            if (BlockExists(block.Index))
                return;

            var batch = new WriteBatch();

            Span<UInt256> txHashes = stackalloc UInt256[block.Transactions.Length];
            for (var x = 0; x < block.Transactions.Length; x++)
            {
                txHashes[x] = block.Transactions[x].CalculateHash();
                PutTransaction(txHashes[x], block.Transactions[x], batch);
            }

            var blockHash = block.CalculateHash();
            PutBlock(block.Header, txHashes, batch);

            db.Write(batch);
        }
    }
}
