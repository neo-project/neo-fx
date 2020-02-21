using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using DevHawk.Buffers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoFx.Models;
using NeoFx.RocksDb;
using NeoFx.Storage;
using RocksDbSharp;

namespace NeoFx.TestNode
{
    // type aliases needed to avoid collision between RocksDbSharp.RocksDb type 
    // and NeoFx.Storage.RocksDb namespace.

    using RocksDb = RocksDbSharp.RocksDb;
    using ColumnFamilies = RocksDbSharp.ColumnFamilies;
    using ColumnFamilyOptions = RocksDbSharp.ColumnFamilyOptions;

    interface IStorage : IDisposable
    {
        (uint index, UInt256 hash) GetLastBlockHash();
        bool TryGetBlockGap(out UInt256 start, out UInt256 stop);
        void AddBlock(in Block block);
    }

    class Storage : IStorage
    {
        const string BLOCKS_FAMILY = "data:blocks";
        const string BLOCK_INDEX_FAMILY = "ix:block-index";
        const string TRANSACTIONS_FAMILY = "data:transactions";

        readonly ILogger<Storage> log;
        private readonly RocksDb db;
        private readonly ColumnFamilyHandle blocksFamily;
        private readonly ColumnFamilyHandle blockIndexFamily;
        private readonly ColumnFamilyHandle transactionsFamily;
        private readonly SortedSet<Block> unverifiedBlocks = new SortedSet<Block>(new BlockComparer());

        public Storage(IOptions<NetworkOptions> networkOptions,
                       IOptions<NodeOptions> nodeOptions,
                       ILogger<Storage>? logger = null)
            : this(nodeOptions.Value.StoragePath, networkOptions.Value.Validators, logger)
        {
        }

        public Storage(string storagePath,
                       string[] validators,
                       ILogger<Storage>? logger = null)
        {
            log = logger ?? NullLogger<Storage>.Instance;

            var columnFamilies = new ColumnFamilies {
                { BLOCKS_FAMILY, new ColumnFamilyOptions() },
                { BLOCK_INDEX_FAMILY, new ColumnFamilyOptions() },
                { TRANSACTIONS_FAMILY, new ColumnFamilyOptions() }};

            var options = new DbOptions()
                .SetCreateIfMissing(true)
                .SetCreateMissingColumnFamilies(true);

            log.LogInformation("Database path {path}", storagePath);

            db = RocksDb.Open(options, storagePath, columnFamilies);
            blocksFamily = db.GetColumnFamily(BLOCKS_FAMILY);
            blockIndexFamily = db.GetColumnFamily(BLOCK_INDEX_FAMILY);
            transactionsFamily = db.GetColumnFamily(TRANSACTIONS_FAMILY);

            if (db.ColumnFamilyEmpty(blockIndexFamily))
            {
                log.LogInformation("Adding Genesis Block");
                var _validators = NetworkOptions.ConvertValidators(validators);
                var genesis = Genesis.CreateGenesisBlock(_validators);
                PutBlock(genesis, true);
            }
        }

        private int alreadyDisposed = 0;
        public void Dispose()
        {
            // work around double dispose issue in RocksDB
            // https://github.com/warrenfalk/rocksdb-sharp/issues/76

            if (Interlocked.Increment(ref alreadyDisposed) == 1)
            {
                db.Dispose();
            }
        }

        public (uint index, UInt256 hash) GetLastBlockHash()
        {
            using var snapshot = db.CreateSnapshot();
            var readOptions = new ReadOptions().SetSnapshot(snapshot);

            var iter = db.NewIterator(blockIndexFamily, readOptions);
            iter.SeekToLast();
            if (iter.Valid()
                && iter.TryGetKey<uint>(BufferReaderExtensions.TryReadBigEndian, out var index)
                && iter.TryGetValue<UInt256>(UInt256.TryRead, out var hash))
            {
                return (index, hash);
            }

            throw new InvalidOperationException("Missing Genesis Block");
        }

        public bool TryGetBlockGap(out UInt256 start, out UInt256 stop)
        {
            if (unverifiedBlocks.Count == 0)
            {
                start = default;
                stop = default;
                return false;
            }

            var firstUnverified = unverifiedBlocks.First();
            var lastVerified = GetLastBlockHash();

            log.LogInformation("TryGetBlockGap {start} {stop}", lastVerified.index, firstUnverified.Index);

            start = lastVerified.hash;
            stop = firstUnverified.CalculateHash();
            return true;
        }

        public void AddBlock(in Block block)
        {
            var (index, _) = GetLastBlockHash();
            if (block.Index <= index)
                return;

            if (index + 1 == block.Index)
            {
                log.LogInformation("Add block {index}", block.Index);
                PutBlock(block);
                ProcessUnverifiedBlocks(block.Index);
            }
            else
            {
                log.LogWarning("Adding Unverified block {index}", block.Index);
                unverifiedBlocks.Add(block);
            }
        }

        private void ProcessUnverifiedBlocks(uint index)
        {
            foreach (var block in unverifiedBlocks)
            {
                if (block.Index <= index)
                    continue;

                if (block.Index == index + 1)
                {
                    log.LogInformation("Processing Unverified block {index}", block.Index);
                    PutBlock(block);
                    index = block.Index;
                    continue;
                }

                break;
            }

            unverifiedBlocks.RemoveWhere(b => b.Index <= index);
        }


        static WriteOptions syncWriteOptions = new WriteOptions().SetSync(true);
        static WriteOptions asyncWriteOptions = new WriteOptions().SetSync(false);

        UInt256 PutBlock(in Block block, bool syncWrite = false)
        {
            var batch = new WriteBatch();

            Span<UInt256> txHashes = stackalloc UInt256[block.Transactions.Length];
            for (var x = 0; x < block.Transactions.Length; x++)
            {
                txHashes[x] = block.Transactions[x].CalculateHash();
                PutTransaction(batch, txHashes[x], block.Transactions[x]);
            }

            var hash = PutTrimmedBlock(batch, block.Header, txHashes);

            var options = syncWrite ? syncWriteOptions : asyncWriteOptions;
            db.Write(batch, options);

            return hash;
        }

        UInt256 PutTrimmedBlock(WriteBatch batch, in BlockHeader header, ReadOnlySpan<UInt256> txHashes)
        {
            Span<byte> indexBuffer = stackalloc byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32BigEndian(indexBuffer, header.Index);

            Span<byte> hashBuffer = stackalloc byte[UInt256.Size];
            var hash = header.CalculateHash();
            hash.Write(hashBuffer);

            var size = header.Size + txHashes.GetVarSize(UInt256.Size);
            using var owner = MemoryPool<byte>.Shared.Rent(size);
            var blockSpan = owner.Memory.Span.Slice(0, size);
            var writer = new BufferWriter<byte>(blockSpan);
            header.WriteTo(ref writer);
            writer.WriteVarArray(txHashes);
            writer.Commit();
            Debug.Assert(writer.Span.IsEmpty);

            batch.Put(blocksFamily, hashBuffer, blockSpan);
            batch.Put(blockIndexFamily, indexBuffer, hashBuffer);
            return hash;
        }

        void PutTransaction(WriteBatch batch, in UInt256 hash, Transaction tx)
        {
            Span<byte> keyBuffer = stackalloc byte[UInt256.Size];
            hash.Write(keyBuffer);

            var size = tx.Size;
            using var owner = MemoryPool<byte>.Shared.Rent(size);
            var txSpan = owner.Memory.Span.Slice(0, size);
            var writer = new BufferWriter<byte>(txSpan);
            tx.WriteTo(ref writer);
            writer.Commit();
            Debug.Assert(writer.Span.IsEmpty);

            batch.Put(transactionsFamily, keyBuffer, txSpan);
        }

        class BlockComparer : IComparer<Block>
        {
            public int Compare(Block x, Block y) => x.Index.CompareTo(y.Index);
        }
    }
}
