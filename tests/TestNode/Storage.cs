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
    class Storage : IStorage, IDisposable
    {
        const string BLOCKS_FAMILY = "data:blocks";
        const string HEADERS_FAMILY = "data:headers";
        const string TRANSACTIONS_FAMILY = "data:transactions";
        const string BLOCK_INDEX_FAMILY = "ix:block-index";
        const string HEADER_INDEX_FAMILY = "ix:header-index";

        readonly RocksDb db;
        readonly ColumnFamilyHandle blocksFamily;
        readonly ColumnFamilyHandle headersFamily;
        readonly ColumnFamilyHandle transactionsFamily;
        readonly ColumnFamilyHandle blockIndexFamily;
        readonly ColumnFamilyHandle headersIndexFamily;

        readonly SortedDictionary<uint, Block> unverifiedBlocks = new SortedDictionary<uint, Block>();
        readonly ILogger<Storage> log;

        public Storage(IOptions<NetworkOptions> networkOptions,
                       IOptions<NodeOptions> nodeOptions,
                       ILogger<Storage>? logger = null)
        {
            log = logger ?? NullLogger<Storage>.Instance;

            var columnFamilies = new ColumnFamilies {
                { BLOCKS_FAMILY, new ColumnFamilyOptions() },
                { HEADERS_FAMILY, new ColumnFamilyOptions() },
                { TRANSACTIONS_FAMILY, new ColumnFamilyOptions() },
                { BLOCK_INDEX_FAMILY, new ColumnFamilyOptions() },
                { HEADER_INDEX_FAMILY, new ColumnFamilyOptions() }
            };

            var options = new DbOptions()
                .SetCreateIfMissing(true)
                .SetCreateMissingColumnFamilies(true);

            var path = nodeOptions.Value.GetStoragePath();
            log.LogInformation("Database path {path}", path);
            db = RocksDb.Open(options, path, columnFamilies);
            blocksFamily = db.GetColumnFamily(BLOCKS_FAMILY);
            headersFamily = db.GetColumnFamily(HEADERS_FAMILY);
            transactionsFamily = db.GetColumnFamily(TRANSACTIONS_FAMILY);
            blockIndexFamily = db.GetColumnFamily(BLOCK_INDEX_FAMILY);
            headersIndexFamily = db.GetColumnFamily(HEADER_INDEX_FAMILY);

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

        public (uint index, UInt256 hash) GetLastHeaderHash()
        {
            if (TryGetLastIndex(db, headersIndexFamily, out var value))
            {
                return value;
            }

            return GetLastBlockHash();
        }

        public (uint index, UInt256 hash) GetLastBlockHash()
        {
            if (TryGetLastIndex(db, blockIndexFamily, out var value))
            {
                return value;
            }

            throw new InvalidOperationException("Missing Genesis Block");
        }

        public void AddBlock(in Block block)
        {
            var (index, hash) = GetLastBlockHash();

            if (index + 1 == block.Index)
            {
                PutBlock(block, true);
            }
            else
            {
                log.LogInformation("Adding Unverified block {index}", block.Index);
                unverifiedBlocks.Add(block.Index, block);
            }
        }

        public void AddHeader(in BlockHeader header)
        {
            var (index, hash) = GetLastBlockHash();
            if (index + 1 == header.Index || index == 0)
            {
                PutHeader(header, true);
            }
        }

        public (UInt256, UInt256) ProcessUnverifiedBlocks()
        {
            if (unverifiedBlocks.Count > 0)
            {
                var (lastBlockIndex, lastBlockHash) = GetLastBlockHash();
                while (unverifiedBlocks.TryGetValue(lastBlockIndex + 1, out var unverifiedBlock))
                {
                    lastBlockHash = PutBlock(unverifiedBlock);
                    lastBlockIndex = unverifiedBlock.Index;
                    unverifiedBlocks.Remove(lastBlockIndex);
                }

                if (unverifiedBlocks.Count > 0)
                {
                    var firstUnverifiedBlock = unverifiedBlocks.Values.First();
                    return (lastBlockHash, firstUnverifiedBlock.CalculateHash());
                }
            }

            return (UInt256.Zero, UInt256.Zero);
        }

        static WriteOptions syncWriteOptions = new WriteOptions().SetSync(true);
        static WriteOptions asyncWriteOptions = new WriteOptions().SetSync(false);

        UInt256 PutBlock(in Block block, bool syncWrite = false)
        {
            log.LogDebug("Put block {index}", block.Index);

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

        UInt256 PutHeader(in BlockHeader header, bool syncWrite = false)
        {
            log.LogDebug("Put BlockHeader {index}", header.Index);

            var batch = new WriteBatch();

            Span<byte> indexBuffer = stackalloc byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32BigEndian(indexBuffer, header.Index);

            Span<byte> hashBuffer = stackalloc byte[UInt256.Size];
            var hash = header.CalculateHash();
            hash.Write(hashBuffer);

            var size = header.Size;
            using var owner = MemoryPool<byte>.Shared.Rent(size);
            var blockSpan = owner.Memory.Span.Slice(0, size);
            var writer = new BufferWriter<byte>(blockSpan);
            header.WriteTo(ref writer);
            writer.Commit();
            Debug.Assert(writer.Span.IsEmpty);

            batch.Put(hashBuffer, blockSpan, headersFamily);
            batch.Put(indexBuffer, hashBuffer, headersIndexFamily);

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

        static bool TryGetLastIndex(RocksDb db, ColumnFamilyHandle columnFamily, out (uint index, UInt256 hash) value)
        {
            using var snapshot = db.CreateSnapshot();
            var readOptions = new ReadOptions().SetSnapshot(snapshot);

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
