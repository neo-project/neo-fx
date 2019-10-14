using NeoFx.Models;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NeoFx.Storage;
using System.Diagnostics.CodeAnalysis;

namespace NeoFx.RocksDb
{
    // type aliases needed to avoid collision between RocksDbSharp.RocksDb type 
    // and NeoFx.Storage.RocksDb namespace.

    using RocksDb = RocksDbSharp.RocksDb;
    using ColumnFamilies = RocksDbSharp.ColumnFamilies;
    using ColumnFamilyOptions = RocksDbSharp.ColumnFamilyOptions;

    public sealed class RocksDbStore : IDisposable, IBlockchainStorage
    {
        public static RocksDbStore OpenCheckpoint(string checkpointPath, string? folder = null)
        {
            folder ??= System.IO.Path.Combine(folder ?? System.IO.Path.GetTempPath(), $"RocksDbStore.{System.IO.Path.GetRandomFileName()}");

            if (!System.IO.File.Exists(checkpointPath))
                throw new ArgumentException(nameof(checkpointPath));

            if (System.IO.Directory.Exists(folder))
            {
                System.IO.Directory.Delete(folder, true);
            }

            System.IO.Compression.ZipFile.ExtractToDirectory(checkpointPath, folder);

            return new RocksDbStore(folder);
        }

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
        private readonly Lazy<UInt256> governingTokenHash;
        private readonly Lazy<UInt256> utilityTokenHash;

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

            governingTokenHash = new Lazy<UInt256>(() => GetTokenHash(AssetType.GoverningToken));
            utilityTokenHash = new Lazy<UInt256>(() => GetTokenHash(AssetType.UtilityToken));
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

        #region IBlockchainStorage

        public uint Height
        {
            get
            {
                if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

                return (uint)blockIndex.Count;
            }
        }

        public UInt256 GoverningTokenHash => governingTokenHash.Value;

        public UInt256 UtilityTokenHash => utilityTokenHash.Value;

        public IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> EnumerateStorage(in UInt160 scriptHash)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            static IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> EnumerateStorage(RocksDb db, UInt160 scriptHash)
            {
                var keyPrefix = new byte[UInt160.Size];
                scriptHash.TryWrite(keyPrefix);

                using var iterator = db.NewIterator(db.GetColumnFamily(STORAGE_FAMILY));
                iterator.Seek(keyPrefix);
                while (iterator.Valid())
                {
                    var keyReadResult = BinaryFormat.TryReadBytes(iterator.Key(), out StorageKey key);
                    var valueReadResult = TryReadStorageItem(iterator.Value(), out var value);

                    Debug.Assert(keyReadResult);
                    Debug.Assert(valueReadResult);

                    yield return (key.Key, value);
                    iterator.Next();
                }
            }

            return EnumerateStorage(db, scriptHash);
        }

        public bool TryGetAccount(in UInt160 key, out Account value)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            if (db.TryGet(ACCOUNT_FAMILY, key, out Account account, UInt160.Size, 2048, TryWriteUInt160Key, TryReadAccountState))
            {
                value = account;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGetAsset(in UInt256 key, out Asset value)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            if (db.TryGet(ASSET_FAMILY, key, out Asset asset, UInt256.Size, 2048, TryWriteUInt256Key, TryReadAssetState))
            {
                value = asset;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGetBlock(in UInt256 key, out Block value)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            if (db.TryGet(BLOCK_FAMILY, key, out (long systemFee, BlockHeader header, ReadOnlyMemory<UInt256> hashes) blockState, UInt256.Size, 2048, TryWriteUInt256Key, TryReadBlockState))
            {
                var hashes = blockState.hashes.Span;
                var transactions = new Transaction[hashes.Length];
                for (int i = 0; i < hashes.Length; i++)
                {
                    if (TryGetTransaction(hashes[i], out var _, out var tx))
                    {
                        transactions[i] = tx;
                    }
                    else
                    {
                        value = default;
                        return false;
                    }
                }

                value = new Block(blockState.header, transactions);
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGetBlock(uint index, out Block value)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            if (index < blockIndex.Count)
            {
                return TryGetBlock(blockIndex[(int)index], out value);
            }

            value = default;
            return false;
        }

        public bool TryGetBlock(in UInt256 key, out BlockHeader header, out ReadOnlyMemory<UInt256> hashes)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            if (db.TryGet(BLOCK_FAMILY, key, out (long systemFee, BlockHeader header, ReadOnlyMemory<UInt256> hashes) value, UInt256.Size, 2048, TryWriteUInt256Key, TryReadBlockState))
            {
                header = value.header;
                hashes = value.hashes;
                return true;
            }

            header = default;
            hashes = default;
            return false;
        }

        public bool TryGetBlockHash(uint index, out UInt256 value)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            if (index < blockIndex.Count)
            {
                value = blockIndex[(int)index];
                return true;
            }

            value = default;
            return true;
        }

        public bool TryGetContract(in UInt160 key, out DeployedContract value)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            if (db.TryGet(CONTRACT_FAMILY, key, out DeployedContract contract, UInt160.Size, 2048, TryWriteUInt160Key, TryReadContractState))
            {
                value = contract;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGetCurrentBlockHash(out UInt256 value)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            if (db.TryGet(METADATA_FAMILY, CURRENT_BLOCK_KEY, out (UInt256 hash, uint index) currentBlock, UInt256.Size + sizeof(uint), TryReadHashIndexState))
            {
                value = currentBlock.hash;
                return true;
            }
            value = currentBlock.hash;
            return true;
        }

        public bool TryGetStorage(in StorageKey key, out StorageItem value)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            static bool TryWriteKey(in StorageKey key, Span<byte> span)
            {
                return key.TryWriteBytes(span);
            }

            if (db.TryGet(STORAGE_FAMILY, key, out StorageItem item, UInt256.Size, 2048, TryWriteKey, TryReadStorageItem))
            {
                value = item;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGetTransaction(in UInt256 key, out uint index, [NotNullWhen(true)] out Transaction? value)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            if (db.TryGet(TX_FAMILY, key, out (uint blockIndex, Transaction tx) txState, UInt256.Size, 2048, TryWriteUInt256Key, TryReadTransactionState))
            {
                index = txState.blockIndex;
                value = txState.tx;
                return true;
            }

            index = default;
            value = default;
            return false;
        }

        public bool TryGetUnspentCoins(in UInt256 key, out ReadOnlyMemory<CoinState> value)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            if (db.TryGet(UNSPENT_COIN_FAMILY, key, out ReadOnlyMemory<CoinState> coins, UInt256.Size, 2048, TryWriteUInt256Key, TryReadUnspentCoinsState))
            {
                value = coins;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGetValidator(in EncodedPublicKey key, out Validator value)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            if (db.TryGet(VALIDATOR_FAMILY, key, out Validator validator, key.Key.Length, 2048, TryWriteEncodedPublicKey, TryReadValidatorState))
            {
                value = validator;
                return true;
            }

            value = default;
            return false;
        }
        #endregion

        public IEnumerable<(UInt256 key, (long systemFee, BlockHeader header, ReadOnlyMemory<UInt256> hashes) blockState)> GetBlocks()
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            return GetBlocks(db);
        }

        public IEnumerable<(UInt256 key, (uint blockIndex, Transaction tx) blockState)> GetTransactions()
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            return GetTransactions(db);
        }

        private UInt256 GetTokenHash(AssetType assetType)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            if (TryGetBlock(blockIndex[0], out var _, out var hashes))
            {
                for (var i = 0; i < hashes.Length; i++)
                {
                    if (TryGetTransaction(hashes.Span[i], out var _, out var tx)
                        && tx is RegisterTransaction register
                        && register.AssetType == assetType)
                    {
                        return hashes.Span[i];
                    }
                }
            }

            throw new InvalidOperationException();
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

        private static bool TryWriteEncodedPublicKey(in EncodedPublicKey key, Span<byte> span)
        {
            Debug.Assert(span.Length == key.Key.Length);
            return key.Key.Span.TryCopyTo(span);
        }

        private static bool TryWriteUInt160Key(in UInt160 key, Span<byte> span)
        {
            Debug.Assert(span.Length == UInt160.Size);
            return key.TryWrite(span);
        }

        private static bool TryWriteUInt256Key(in UInt256 key, Span<byte> span)
        {
            Debug.Assert(span.Length == UInt256.Size);
            return key.TryWrite(span);
        }

        private static bool TryReadAccountState(ReadOnlyMemory<byte> memory, out Account value)
        {
            var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(memory));

            if (TryReadStateVersion(ref reader, 0)
                && reader.TryRead(out Account account))
            {
                Debug.Assert(reader.Remaining == 0);

                value = account;
                return true;
            }

            value = default;
            return false;
        }

        private static bool TryReadAssetState(ReadOnlyMemory<byte> memory, out Asset value)
        {
            var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(memory));

            if (TryReadStateVersion(ref reader, 0)
                && reader.TryRead(out Asset asset))
            {
                Debug.Assert(reader.Remaining == 0);

                value = asset;
                return true;
            }

            value = default;
            return false;
        }

        private static bool TryReadBlockState(ReadOnlyMemory<byte> memory, out (long systemFee, BlockHeader header, ReadOnlyMemory<UInt256> hashes) value)
        {
            var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(memory));

            if (TryReadStateVersion(ref reader, 0)
                && reader.TryRead(out long systemFee)
                && reader.TryRead(out BlockHeader header)
                && reader.TryReadVarArray<UInt256>(BinaryFormat.TryRead, out var hashes))
            {
                Debug.Assert(reader.Remaining == 0);
                value = (systemFee, header, hashes);
                return true;
            }

            value = default;
            return false;
        }

        private static bool TryReadContractState(ReadOnlyMemory<byte> memory, out DeployedContract value)
        {
            var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(memory));

            if (TryReadStateVersion(ref reader, 0)
                && reader.TryRead(out DeployedContract contract))
            {
                Debug.Assert(reader.Remaining == 0);

                value = contract;
                return true;
            }

            value = default;
            return false;
        }

        private static bool TryReadHashIndexState(ReadOnlyMemory<byte> memory, out (UInt256 hash, uint index) value)
        {
            var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(memory));
            if (TryReadStateVersion(ref reader, 0)
                && reader.TryRead(out UInt256 hash)
                && reader.TryRead(out uint index))
            {
                Debug.Assert(reader.Remaining == 0);

                value = (hash, index);
                return true;
            }

            value = default;
            return false;
        }

        private static bool TryReadStorageItem(ReadOnlyMemory<byte> memory, out StorageItem value)
        {
            var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(memory));

            if (TryReadStateVersion(ref reader, 0)
                && reader.TryRead(out StorageItem item))
            {
                Debug.Assert(reader.Remaining == 0);

                value = item;
                return true;
            }

            value = default;
            return false;
        }

        private static bool TryReadTransactionState(ReadOnlyMemory<byte> memory, out (uint blockIndex, Transaction tx) value)
        {
            var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(memory));

            if (TryReadStateVersion(ref reader, 0)
                && reader.TryRead(out uint blockIndex)
                && reader.TryRead(out Transaction? tx))
            {
                Debug.Assert(reader.Remaining == 0);
                value = (blockIndex, tx);
                return true;
            }

            value = default;
            return false;
        }

        private static bool TryReadUnspentCoinsState(ReadOnlyMemory<byte> memory, out ReadOnlyMemory<CoinState> value)
        {
            var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(memory));

            if (TryReadStateVersion(ref reader, 0)
                && reader.TryReadVarArray(BinaryFormat.TryRead, out ReadOnlyMemory<CoinState> coins))
            {
                Debug.Assert(reader.Remaining == 0);

                value = coins;
                return true;
            }

            value = default;
            return false;
        }

        private static bool TryReadValidatorState(ReadOnlyMemory<byte> memory, out Validator value)
        {
            var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(memory));

            if (TryReadStateVersion(ref reader, 0)
                && reader.TryRead(out Validator validator))
            {
                Debug.Assert(reader.Remaining == 0);

                value = validator;
                return true;
            }

            value = default;
            return false;
        }

        private static IEnumerable<(UInt256 key, (long systemFee, BlockHeader header, ReadOnlyMemory<UInt256> hashes) blockState)> GetBlocks(RocksDb db)
        {
            return db.Iterate<UInt256, (long, BlockHeader, ReadOnlyMemory<UInt256>)>(BLOCK_FAMILY, TryReadUInt256Key, TryReadBlockState);
        }

        private static IEnumerable<(UInt256 key, (uint blockIndex, Transaction tx) blockState)> GetTransactions(RocksDb db)
        {
            return db.Iterate<UInt256, (uint, Transaction)>(TX_FAMILY, TryReadUInt256Key, TryReadTransactionState);
        }
    }
}
