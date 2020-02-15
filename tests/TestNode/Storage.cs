using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using DevHawk.Buffers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoFx;
using NeoFx.Models;
using NeoFx.Storage;
using RocksDbSharp;

namespace NeoFx.TestNode
{
    interface IStorage
    {
        (uint index, UInt256 hash) GetLastBlockHash();
        void AddBlock(in Block block);
    }

    class Storage : IStorage, IDisposable
    {
        const string BLOCKS_FAMILY = "data:blocks";
        const string TRANSACTIONS_FAMILY = "data:transactions";
        const string BLOCK_INDEX_FAMILY = "ix:block-index";

        readonly RocksDb db;
        readonly ColumnFamilyHandle blocksFamily;
        readonly ColumnFamilyHandle transactionsFamily;
        readonly ColumnFamilyHandle blockIndexFamily;
        readonly SortedDictionary<uint, Block> unverifiedBlocks = new SortedDictionary<uint, Block>();
        readonly ILogger<Storage> log;

        public Storage(IOptions<NetworkOptions> networkOptions,
                       IOptions<NodeOptions> nodeOptions,
                       ILogger<Storage>? logger = null)
        {
            log = logger ?? NullLogger<Storage>.Instance;

            var columnFamilies = new ColumnFamilies {
                { BLOCKS_FAMILY, new ColumnFamilyOptions() },
                { TRANSACTIONS_FAMILY, new ColumnFamilyOptions() },
                { BLOCK_INDEX_FAMILY, new ColumnFamilyOptions() },
            };

            var options = new DbOptions()
                .SetCreateIfMissing(true)
                .SetCreateMissingColumnFamilies(true);

            var path = nodeOptions.Value.GetStoragePath();
            log.LogInformation("Database path {path}", path);
            db = RocksDb.Open(options, path, columnFamilies);
            blocksFamily = db.GetColumnFamily(BLOCKS_FAMILY);
            transactionsFamily = db.GetColumnFamily(TRANSACTIONS_FAMILY);
            blockIndexFamily = db.GetColumnFamily(BLOCK_INDEX_FAMILY);

            if (!BlockExists(0))
            {
                log.LogInformation("Adding Genesis Block");
                AddBlock(networkOptions.Value.GetGenesisBlock());
            }
        }

        public void Dispose()
        {
            db.Dispose();
        }

        bool TryGetBlockHash(out (uint index, UInt256 hash) value)
        {
            using var snapshot = db.CreateSnapshot();
            var readOptions = new ReadOptions().SetSnapshot(snapshot);

            var iter = db.NewIterator(blockIndexFamily, readOptions);
            iter.SeekToLast();

            if (iter.Valid())
            {
                IntPtr keyPtr = Native.Instance.rocksdb_iter_key(iter.Handle, out UIntPtr keyLength);
                IntPtr valuePtr = Native.Instance.rocksdb_iter_value(iter.Handle, out UIntPtr valueLength);

                if (RocksDbExtensions.TryConvert<uint>(keyPtr, keyLength, BinaryPrimitives.TryReadUInt32BigEndian, out var index)
                    && RocksDbExtensions.TryConvert<UInt256>(valuePtr, valueLength, UInt256.TryRead, out var hash))
                {
                    value = (index, hash);
                    return true;
                }
            }

            value = default;
            return false;
        }

        public (uint index, UInt256 hash) GetLastBlockHash()
        {
            if (TryGetBlockHash(out var value))
            {
                return value;
            }

            throw new InvalidOperationException();
        }

        static WriteOptions syncWriteOptions = new WriteOptions().SetSync(true);

        void PutBlock(in Block block)
        {
            log.LogDebug("Put block {index}", block.Index);

            var batch = new WriteBatch();

            Span<UInt256> txHashes = stackalloc UInt256[block.Transactions.Length];
            for (var x = 0; x < block.Transactions.Length; x++)
            {
                txHashes[x] = block.Transactions[x].CalculateHash();
                PutTransaction(txHashes[x], block.Transactions[x], batch);
            }

            PutBlock(block.Header, txHashes, batch);

            db.Write(batch);
        }

        public void AddBlock(in Block block)
        {
            if (TryGetBlockHash(out var value))
            {
                if (value.index + 1 == block.Index)
                {
                    PutBlock(block);
                }
                else
                {
                    log.LogInformation("Adding Unverified block {index}", block.Index);
                    unverifiedBlocks.Add(block.Index, block);

                    var firstUnverified = unverifiedBlocks.Keys.First();
                    log.LogWarning("current block {index} {index2}", value.index, firstUnverified);
                    return;
                }
            }
            else
            {
                if (block.Index != 0)
                {
                    throw new Exception("Missing Genesis Block");
                }
            }


            if (unverifiedBlocks.Count > 0)
            {
                var lastBlockIndex = block.Index;
                while (unverifiedBlocks.TryGetValue(lastBlockIndex, out var unverifiedBlock))
                {
                    PutBlock(unverifiedBlock);
                    unverifiedBlocks.Remove(lastBlockIndex);
                    lastBlockIndex = unverifiedBlock.Index;
                }
            }
        }

        // (uint index, UInt256 hash) GetLastBlockHash()
        // {
        //     using var snapshot = db.CreateSnapshot();
        //     var readOptions = new ReadOptions().SetSnapshot(snapshot);
        //     var iter = db.NewIterator(blockIndexFamily, readOptions);
        //     iter.SeekToLast();
        //     unsafe 
        //     {

        //     }
        //     iter.
        //     return TryGetValueHash(iter, out hash);

        // }


        public IEnumerable<UInt256> FilterBlocks(ImmutableArray<UInt256> blockHashes)
        {
            using var snapshot = db.CreateSnapshot();
            var readOptions = new ReadOptions().SetSnapshot(snapshot);

            for (var i = 0; i < blockHashes.Length; i++)
            {
                if (!BlockExists(blockHashes[i]))
                {
                    yield return blockHashes[i];
                }
            }
        }

        bool BlockExists(uint index, ReadOptions? readOptions = null)
        {
            Span<byte> indexBuffer = stackalloc byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32BigEndian(indexBuffer, index);
            return db.KeyExists(indexBuffer, blockIndexFamily, readOptions);
        }

        bool BlockExists(in UInt256 hash, ReadOptions? readOptions = null)
        {
            Span<byte> indexBuffer = stackalloc byte[UInt256.Size];
            hash.Write(indexBuffer);
            return db.KeyExists(indexBuffer, blocksFamily, readOptions);
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

            batch.Put(hashBuffer, blockSpan, blocksFamily);
            batch.Put(indexBuffer, hashBuffer, blockIndexFamily);
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

            batch.Put(keyBuffer, txSpan, transactionsFamily);
        }
    }
}
