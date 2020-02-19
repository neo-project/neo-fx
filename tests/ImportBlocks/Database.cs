using DevHawk.Buffers;
using NeoFx;
using NeoFx.Models;
using NeoFx.Storage;
using NeoFx.RocksDb;
using RocksDbSharp;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace ImportBlocks
{
    class Database : IDisposable
    {
        const string BLOCKS_FAMILY = "data:blocks";
        const string BLOCK_INDEX_FAMILY = "ix:block-index";
        const string TRANSACTIONS_FAMILY = "data:transactions";

        private readonly RocksDb db;
        private readonly ColumnFamilyHandle blocksFamily;
        private readonly ColumnFamilyHandle blockIndexFamily;
        private readonly ColumnFamilyHandle transactionsFamily;

        public Database(string path)
        {
            var columnFamilies = new ColumnFamilies {
                { BLOCKS_FAMILY, new ColumnFamilyOptions() },
                { BLOCK_INDEX_FAMILY, new ColumnFamilyOptions() },
                { TRANSACTIONS_FAMILY, new ColumnFamilyOptions() }};

            var options = new DbOptions()
                .SetCreateIfMissing(true)
                .SetCreateMissingColumnFamilies(true);

            db = RocksDb.Open(options, path, columnFamilies);
            blocksFamily = db.GetColumnFamily(BLOCKS_FAMILY);
            blockIndexFamily = db.GetColumnFamily(BLOCK_INDEX_FAMILY);
            transactionsFamily = db.GetColumnFamily(TRANSACTIONS_FAMILY);
        }

        public void Dispose()
        {
            db.Dispose();
        }

        private readonly struct BlockStateFactory : IFactoryReader<(BlockHeader header, ImmutableArray<UInt256> hashes)>
        {
            public bool TryReadItem(ref BufferReader<byte> reader, out (BlockHeader header, ImmutableArray<UInt256> hashes) value)
            {
                if (BlockHeader.TryRead(ref reader, out var header)
                    && reader.TryReadVarArray<UInt256, UInt256.Factory>(out var hashes))
                {
                    Debug.Assert(reader.Remaining == 0);
                    value = (header, hashes);
                    return true;
                }

                value = default;
                return false;
            }
        }
        public IEnumerable<(UInt256 key, (BlockHeader header, ImmutableArray<UInt256> hashes) blockState)> GetBlocks()
        {
            return db.Iterate<UInt256, UInt256.Factory, (BlockHeader, ImmutableArray<UInt256>), BlockStateFactory>(blocksFamily);
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

            batch.Put(transactionsFamily, keyBuffer, txSpan);
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

            batch.Put(blocksFamily, hashBuffer, blockSpan);
            batch.Put(blockIndexFamily, hashBuffer, hashBuffer);
        }

        bool BlockExists(uint index)
        {
            Span<byte> indexBuffer = stackalloc byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32BigEndian(indexBuffer, index);
            return db.KeyExists(blockIndexFamily, indexBuffer);
        }

        static WriteOptions syncWriteOptions = new WriteOptions().SetSync(true);
        static WriteOptions asyncWriteOptions = new WriteOptions().SetSync(false);

        public void AddBlock(in Block block)
        {
            if (BlockExists(block.Index))
            {
                Console.WriteLine($"skipping Block {block.Index}");
                return;
            }

            var batch = new WriteBatch();

            Span<UInt256> txHashes = stackalloc UInt256[block.Transactions.Length];
            for (var x = 0; x < block.Transactions.Length; x++)
            {
                txHashes[x] = block.Transactions[x].CalculateHash();
                PutTransaction(txHashes[x], block.Transactions[x], batch);
            }

            var blockHash = block.CalculateHash();
            PutBlock(block.Header, txHashes, batch);

            db.Write(batch, syncWriteOptions);
        }
    }
}
