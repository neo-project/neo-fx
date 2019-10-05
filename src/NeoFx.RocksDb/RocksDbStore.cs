using NeoFx.Models;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NeoFx.Storage;

namespace NeoFx.RocksDb
{
    // type aliases needed to avoid collision between RocksDbSharp.RocksDb type 
    // and NeoFx.Storage.RocksDb namespace.

    using RocksDb = RocksDbSharp.RocksDb;
    using ColumnFamilies = RocksDbSharp.ColumnFamilies;
    using ColumnFamilyOptions = RocksDbSharp.ColumnFamilyOptions;

    public sealed class RocksDbStore : IDisposable, IBlockchainStorage
    {
        #region Column Family Constants
        private const string BLOCK_FAMILY = "data:block";
        private const string TX_FAMILY = "data:transaction";
        private const string ACCOUNT_FAMILY = "st:account";
        private const string ASSET_FAMILY = "st:asset";
        private const string CONTRACT_FAMILY = "st:contract";
        private const string HEADER_HASH_LIST_FAMILY = "ix:header-hash-list";
        private const string SPENT_COIN_FAMILY = "st:spent-coin";
        private const string STORAGE_FAMILY = "st:storage";
        private const string UNSPENT_COIN_FAMILY = "st:coin";
        private const string VALIDATOR_FAMILY = "st:validator";
        private const string METADATA_FAMILY = "metadata";
        private const string GENERAL_STORAGE_FAMILY = "general-storage";

        private const byte VALIDATORS_COUNT_KEY = 0x90;
        private const byte CURRENT_BLOCK_KEY = 0xc0;
        private const byte CURRENT_HEADER_KEY = 0xc1;

        private static ColumnFamilies ColumnFamilies => new ColumnFamilies {
                { BLOCK_FAMILY, new ColumnFamilyOptions() },
                { TX_FAMILY, new ColumnFamilyOptions() },
                { ACCOUNT_FAMILY, new ColumnFamilyOptions() },
                { UNSPENT_COIN_FAMILY, new ColumnFamilyOptions() },
                { SPENT_COIN_FAMILY, new ColumnFamilyOptions() },
                { VALIDATOR_FAMILY, new ColumnFamilyOptions() },
                { ASSET_FAMILY, new ColumnFamilyOptions() },
                { CONTRACT_FAMILY, new ColumnFamilyOptions() },
                { STORAGE_FAMILY, new ColumnFamilyOptions() },
                { HEADER_HASH_LIST_FAMILY, new ColumnFamilyOptions() },
                { METADATA_FAMILY, new ColumnFamilyOptions() },
                { GENERAL_STORAGE_FAMILY, new ColumnFamilyOptions() }};
        #endregion

        private readonly RocksDb db;
        private readonly IList<UInt256> blockIndex;

        public RocksDbStore(string path)
        {
            var options = new RocksDbSharp.DbOptions()
                .SetCreateIfMissing(false)
                .SetCreateMissingColumnFamilies(false);

            db = RocksDb.Open(options, path, ColumnFamilies);
            blockIndex = GetBlocks(db)
                .OrderBy(t => t.blockState.header.Index)
                .Select(t => t.key)
                .ToList();
        }

        private bool objectDisposed = false;

        public void Dispose()
        {
            if (!objectDisposed)
            {
                db.Dispose();
                blockIndex.Clear();
                objectDisposed = true;
            }
        }

        public uint Height
        {
            get
            {
                if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

                return (uint)blockIndex.Count;
            }
        }

        public bool TryGetBlock(in UInt256 key, out Block block)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            if (db.TryGet(BLOCK_FAMILY, key, out (long systemFee, BlockHeader header, ReadOnlyMemory<UInt256> hashes) value, UInt256.Size, 2048, TryWriteUInt256Key, TryReadBlockState))
            {
                var hashes = value.hashes.Span;
                var transactions = new Transaction[hashes.Length];
                for (int i = 0; i < hashes.Length; i++)
                {
                    if (!TryGetTransaction(hashes[i], out var _, out transactions[i]))
                    {
                        block = default;
                        return false;
                    }
                }

                block = new Block(value.header, transactions);
                return true;
            }

            block = default;
            return false;
        }

        public bool TryGetBlock(uint index, out Block block)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            if (index < blockIndex.Count)
            {
                return TryGetBlock(blockIndex[(int)index], out block);
            }

            block = default;
            return false;
        }

        public bool TryGetTransaction(in UInt256 key, out uint index, out Transaction tx)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            if (db.TryGet(TX_FAMILY, key, out (uint blockIndex, Transaction tx) value, UInt256.Size, 2048, TryWriteUInt256Key, TryReadTransactionState))
            {
                index = value.blockIndex;
                tx = value.tx;
                return true;
            }

            index = default;
            tx = default;
            return false;
        }

        public bool TryGetStorage(in StorageKey key, out StorageItem item)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            static bool TryWriteKey(in StorageKey key, Span<byte> span)
            {
                return key.TryWrite(span);
            }

            if (db.TryGet(STORAGE_FAMILY, key, out StorageItem value, UInt256.Size, 2048, TryWriteKey, TryReadStorageItem))
            {
                item = value;
                return true;
            }

            item = default;
            return false;
        }

        public IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> EnumerateStorage(in UInt160 scriptHash)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            static IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> EnumerateStorage(RocksDb db, UInt160 scriptHash)
            {
                var keyPrefix = new byte[UInt160.Size];
                scriptHash.TryWriteBytes(keyPrefix);

                using var iterator = db.NewIterator(db.GetColumnFamily(STORAGE_FAMILY));
                iterator.Seek(keyPrefix);
                while (iterator.Valid())
                {
                    var keyReadResult = StorageKey.TryRead(iterator.Key(), out var key);
                    var valueReadResult = TryReadStorageItem(iterator.Value(), out var value);

                    Debug.Assert(keyReadResult);
                    Debug.Assert(valueReadResult);

                    yield return (key.Key, value);
                    iterator.Next();
                }
            }

            return EnumerateStorage(db, scriptHash);
        }

        private static bool TryReadStateVersion(ref SequenceReader<byte> reader, byte expectedVersion)
        {
            if (reader.TryPeek(out var value) && value == expectedVersion)
            {
                reader.Advance(sizeof(byte));
                return true;
            }

            return false;
        }

        private static bool TryReadUInt256Key(ReadOnlyMemory<byte> memory, out UInt256 key)
        {
            Debug.Assert(memory.Length == UInt256.Size);
            return UInt256.TryRead(memory.Span, out key);
        }

        private static bool TryWriteUInt256Key(in UInt256 key, Span<byte> span)
        {
            Debug.Assert(span.Length == UInt256.Size);
            return key.TryWriteBytes(span);
        }

        private static bool TryReadBlockState(ReadOnlyMemory<byte> memory, out (long systemFee, BlockHeader header, ReadOnlyMemory<UInt256> hashes) value)
        {
            //var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(memory));

            //if (TryReadStateVersion(ref reader, 0)
            //    && reader.TryReadInt64LittleEndian(out var systemFee)
            //    && reader.TryRead(out BlockHeader header)
            //    && reader.TryReadVarArray<UInt256>(SequenceReaderExtensions.TryReadUInt256, out var hashes))
            //{
            //    Debug.Assert(reader.Remaining == 0);
            //    value = (systemFee, header, hashes);
            //    return true;
            //}

            value = default;
            return false;
        }

        private static bool TryReadTransactionState(ReadOnlyMemory<byte> memory, out (uint blockIndex, Transaction tx) value)
        {
            //var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(memory));

            //if (TryReadStateVersion(ref reader, 0)
            //    && reader.TryReadUInt32LittleEndian(out var blockIndex)
            //    && Transaction.TryRead(ref reader, out var tx))
            //{
            //    Debug.Assert(reader.Remaining == 0);
            //    value = (blockIndex, tx);
            //    return true;
            //}

            value = default;
            return false;
        }

        private static bool TryReadStorageItem(ReadOnlyMemory<byte> memory, out StorageItem value)
        {
            var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(memory));

            if (TryReadStateVersion(ref reader, 0)
                && StorageItem.TryRead(ref reader, out var item))
            {
                Debug.Assert(reader.Remaining == 0);

                value = item;
                return true;
            }

            value = default;
            return false;
        }

        private static IEnumerable<(UInt256 key, (long systemFee, BlockHeader header, ReadOnlyMemory<UInt256> hashes) blockState)> GetBlocks(RocksDb db)
        {
            return db.Iterate<UInt256, (long, BlockHeader, ReadOnlyMemory<UInt256>)>(BLOCK_FAMILY, TryReadUInt256Key, TryReadBlockState);
        }

        private static IEnumerable<(UInt256 key, (uint blockIndex, Transaction tx) txState)> GetTransactions(RocksDb db)
        {
            return db.Iterate<UInt256, (uint, Transaction)>(TX_FAMILY, TryReadUInt256Key, TryReadTransactionState);
        }
    }
}
