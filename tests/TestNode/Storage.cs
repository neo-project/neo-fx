using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DevHawk.Buffers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoFx.Models;
using NeoFx.Storage;
using RocksDbSharp;

namespace NeoFx.TestNode
{
    class Storage : IDisposable
    {
        const string BLOCKS_FAMILY = "data:blocks";
        const string BLOCK_INDEX_FAMILY = "ix:block-index";
        const string TRANSACTIONS_FAMILY = "data:transactions";

        readonly ILogger<Storage> log;
        private readonly RocksDb db;
        private readonly ColumnFamilyHandle blocksFamily;
        private readonly ColumnFamilyHandle blockIndexFamily;
        private readonly ColumnFamilyHandle transactionsFamily;
        private readonly SortedDictionary<uint, Block> unverifiedBlocks = new SortedDictionary<uint, Block>();

        public Storage(IOptions<NetworkOptions> networkOptions,
                       IOptions<NodeOptions> nodeOptions,
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

            var path = nodeOptions.Value.GetStoragePath();
            log.LogInformation("Database path {path}", path);

            db = RocksDb.Open(options, path, columnFamilies);
            blocksFamily = db.GetColumnFamily(BLOCKS_FAMILY);
            blockIndexFamily = db.GetColumnFamily(BLOCK_INDEX_FAMILY);
            transactionsFamily = db.GetColumnFamily(TRANSACTIONS_FAMILY);

            if (db.ColumnFamilyEmpty(blockIndexFamily))
            {
                log.LogInformation("Adding Genesis Block");
                PutBlock(networkOptions.Value.GetGenesisBlock(), true);
            }
        }

        public void Dispose()
        {
            db.Dispose();
        }

        public (uint index, UInt256 hash) GetLastBlockHash()
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
                    return (index, hash);
                }
            }

            throw new InvalidOperationException("Missing Genesis Block");
        }

        private uint GetLastBlockIndex()
        {
            using var snapshot = db.CreateSnapshot();
            var readOptions = new ReadOptions().SetSnapshot(snapshot);

            var iter = db.NewIterator(blockIndexFamily, readOptions);
            iter.SeekToLast();
            if (iter.Valid())
            {
                IntPtr keyPtr = Native.Instance.rocksdb_iter_key(iter.Handle, out UIntPtr keyLength);

                if (RocksDbExtensions.TryConvert<uint>(keyPtr, keyLength, BinaryPrimitives.TryReadUInt32BigEndian, out var index))
                {
                    return index;
                }
            }

            throw new InvalidOperationException("Missing Genesis Block");
        }

        public void AddBlock(in Block block)
        {
            var index = GetLastBlockIndex();
            if (block.Index <= index)
                return;

            if (index + 1 == block.Index)
            {
                PutBlock(block, true);
            }
            else
            {
                log.LogWarning("Adding Unverified block {index}", block.Index);
                unverifiedBlocks.Add(block.Index, block);
            }
        }

        public void Cleanup()
        {
            var index = GetLastBlockIndex();
            index = ProcessUnverifiedBlocks(index); 
            RemoveProcessedUnverifiedBlocks(index);
        }

        private uint ProcessUnverifiedBlocks(uint index)
        {
            foreach (var kvp in unverifiedBlocks)
            {
                if (kvp.Key <= index)
                    continue;

                if (kvp.Key == index + 1)
                {
                        log.LogInformation("Processing Unverified block {index}", kvp.Key);
                        PutBlock(kvp.Value);
                        index = kvp.Key;
                        continue;
                }
                
                break;
            }

            return index;
        }

        private void RemoveProcessedUnverifiedBlocks(uint index)
        {
            var processedKeys = unverifiedBlocks.Keys.TakeWhile(i => i <= index).ToList();
            for (var x = 0; x < processedKeys.Count; x++)
            {
                unverifiedBlocks.Remove(processedKeys[x]);
            }
        }

        static WriteOptions syncWriteOptions = new WriteOptions().SetSync(true);
        static WriteOptions asyncWriteOptions = new WriteOptions().SetSync(false);

        UInt256 PutBlock(in Block block, bool syncWrite = false)
        {
            log.LogInformation("Put block {index}", block.Index);

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

            batch.Put(hashBuffer, blockSpan, blocksFamily);
            batch.Put(indexBuffer, hashBuffer, blockIndexFamily);
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

            batch.Put(keyBuffer, txSpan, transactionsFamily);
        }

        static bool TryGetLastIndex(RocksDb db, ColumnFamilyHandle columnFamily, ReadOptions readOptions, out (uint index, UInt256 hash) value)
        {
            var iter = db.NewIterator(columnFamily, readOptions);
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
    }
}
