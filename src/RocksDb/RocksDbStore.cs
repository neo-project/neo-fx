using DevHawk.Buffers;
using NeoFx.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NeoFx.Storage;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

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
            {
                throw new Exception($"{checkpointPath} checkpoint file could not be found");
            }

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
        private bool objectDisposed = false;

        private UInt256 GetTokenHash(AssetType assetType)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            if (TryGetBlock(blockIndex[0], out var _, out var hashes))
            {
                for (var i = 0; i < hashes.Length; i++)
                {
                    if (TryGetTransaction(hashes[i], out var _, out var tx)
                        && tx is RegisterTransaction register
                        && register.AssetType == assetType)
                    {
                        return hashes[i];
                    }
                }
            }

            throw new InvalidOperationException();
        }

        public RocksDbStore(string path)
        {
            var options = new RocksDbSharp.DbOptions()
                .SetCreateIfMissing(false)
                .SetCreateMissingColumnFamilies(false);

            db = RocksDb.Open(options, path, ColumnFamilies);
            blockIndex = GetBlocks()
                .OrderBy(t => t.blockState.header.Index)
                .Select(t => t.key)
                .ToList();

            governingTokenHash = new Lazy<UInt256>(() => GetTokenHash(AssetType.GoverningToken));
            utilityTokenHash = new Lazy<UInt256>(() => GetTokenHash(AssetType.UtilityToken));
        }

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

                return (uint)blockIndex.Count - 1;
            }
        }

        public UInt256 GoverningTokenHash => governingTokenHash.Value;

        public UInt256 UtilityTokenHash => utilityTokenHash.Value;

        public IEnumerable<(ImmutableArray<byte> key, StorageItem item)> EnumerateStorage(in UInt160 scriptHash)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            var columnFamily = db.GetColumnFamily(STORAGE_FAMILY);
            Span<byte> keyPrefix = stackalloc byte[UInt160.Size];
            scriptHash.Write(keyPrefix);

            return db.Search<StorageKey, StorageItem>(columnFamily, keyPrefix, StorageKey.TryRead, TryReadStorageItemState)
                .Select(t => (t.key.Key, t.value));
        }

        public bool TryGetAccount(in UInt160 key, out Account value)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            var columnFamily = db.GetColumnFamily(ACCOUNT_FAMILY);
            Span<byte> keybuffer = stackalloc byte[UInt160.Size];

            if (key.TryWrite(keybuffer)
                && db.TryGet<Account>(keybuffer, columnFamily, TryReadAccountState, out Account account))
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

            var columnFamily = db.GetColumnFamily(ACCOUNT_FAMILY);
            Span<byte> keybuffer = stackalloc byte[UInt256.Size];

            // TODO: get key buffer w/o copy
            //  Span<UInt256> keySpan = stackalloc UInt256[] { key };
            //  Span<byte> keyBuffer = MemoryMarshal.AsBytes(keySpan).Slice(0, UInt256.Size);

            if (key.TryWrite(keybuffer)
                && db.TryGet<Asset>(keybuffer, columnFamily, TryReadAssetState, out Asset asset))
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

            if (TryGetBlock(key, out BlockHeader header, out var hashes))
            {
                var transactions = ImmutableArray.CreateBuilder<Transaction>(hashes.Length);
                for (int i = 0; i < hashes.Length; i++)
                {
                    if (TryGetTransaction(hashes[i], out var _, out var tx))
                    {
                        transactions.Add(tx);
                    }
                    else
                    {
                        value = default;
                        return false;
                    }
                }

                value = new Block(header, transactions.MoveToImmutable());
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

        public bool TryGetBlock(in UInt256 key, out BlockHeader header, out ImmutableArray<UInt256> hashes)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            var columnFamily = db.GetColumnFamily(BLOCK_FAMILY);
            Span<byte> keybuffer = stackalloc byte[UInt256.Size];

            if (key.TryWrite(keybuffer)
                && db.TryGet<(long systemFee, BlockHeader header, ImmutableArray<UInt256> hashes)>(keybuffer, columnFamily, TryReadBlockState, out var value))
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

            var columnFamily = db.GetColumnFamily(CONTRACT_FAMILY);
            Span<byte> keybuffer = stackalloc byte[UInt160.Size];

            if (key.TryWrite(keybuffer)
                && db.TryGet<DeployedContract>(keybuffer, columnFamily, DeployedContract.TryRead, out DeployedContract contract))
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

            var columnFamily = db.GetColumnFamily(METADATA_FAMILY);
            Span<byte> keybuffer = stackalloc byte[1];
            keybuffer[0] = CURRENT_BLOCK_KEY;

            if (db.TryGet<(UInt256 hash, uint index)>(keybuffer, columnFamily, TryReadCurrentBlockHashState, out var currentBlock))
            {
                value = currentBlock.hash;
                return false;
            }

            value = default;
            return false;

        }

        public bool TryGetStorage(in StorageKey key, out StorageItem value)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            var columnFamily = db.GetColumnFamily(STORAGE_FAMILY);
            Span<byte> keybuffer = stackalloc byte[key.Size];

            if (key.TryWrite(keybuffer, out var _)
                && db.TryGet<StorageItem>(keybuffer, columnFamily, TryReadStorageItemState, out StorageItem item))
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

            var columnFamily = db.GetColumnFamily(TX_FAMILY);
            Span<byte> keybuffer = stackalloc byte[UInt256.Size];

            if (key.TryWrite(keybuffer)
                && db.TryGet<(uint blockIndex, Transaction tx)>(keybuffer, columnFamily, TryReadTransactionState, out var txState))
            {
                index = txState.blockIndex;
                value = txState.tx;
                return true;
            }

            index = default;
            value = default;
            return false;
        }

        public bool TryGetUnspentCoins(in UInt256 key, out ImmutableArray<CoinState> value)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            var columnFamily = db.GetColumnFamily(UNSPENT_COIN_FAMILY);
            Span<byte> keybuffer = stackalloc byte[UInt256.Size];

            if (key.TryWrite(keybuffer)
                && db.TryGet<ImmutableArray<CoinState>>(keybuffer, columnFamily, TryReadUnspentCoinState, out var item))
            {
                value = item;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGetValidator(in EncodedPublicKey key, out Validator value)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            var columnFamily = db.GetColumnFamily(VALIDATOR_FAMILY);
            if (db.TryGet<Validator>(key.Key.AsSpan(), columnFamily, TryReadValidatorState, out Validator validator))
            {
                value = validator;
                return true;
            }

            value = default;
            return false;
        }
        #endregion

        public IEnumerable<(UInt160 key, Account accountState)> GetAccounts()
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            var columnFamily = db.GetColumnFamily(ACCOUNT_FAMILY);
            return db.Iterate<UInt160, Account>(columnFamily, UInt160.TryRead, TryReadAccountState);
        }

        public IEnumerable<(UInt256 key, Asset assetState)> GetAssets()
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            var columnFamily = db.GetColumnFamily(ASSET_FAMILY);
            return db.Iterate<UInt256, Asset>(columnFamily, UInt256.TryRead, TryReadAssetState);
        }

        public IEnumerable<(UInt256 key, (long systemFee, BlockHeader header, ImmutableArray<UInt256> hashes) blockState)> GetBlocks()
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            var columnFamily = db.GetColumnFamily(BLOCK_FAMILY);
            return db.Iterate<UInt256, (long, BlockHeader, ImmutableArray<UInt256>)>(columnFamily, UInt256.TryRead, TryReadBlockState);
        }

        public IEnumerable<(UInt160 key, DeployedContract contractState)> GetContracts()
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            var columnFamily = db.GetColumnFamily(CONTRACT_FAMILY);
            return db.Iterate<UInt160, DeployedContract>(columnFamily, UInt160.TryRead, TryReadContractState);
        }

        public IEnumerable<(StorageKey key, StorageItem storageItem)> GetStorages()
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            var columnFamily = db.GetColumnFamily(STORAGE_FAMILY);
            return db.Iterate<StorageKey, StorageItem>(columnFamily, StorageKey.TryRead, TryReadStorageItemState);
        }

        public IEnumerable<(UInt256 key, (uint blockIndex, Transaction tx) transactionState)> GetTransactions()
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            var columnFamily = db.GetColumnFamily(TX_FAMILY);
            return db.Iterate<UInt256, (uint, Transaction)>(columnFamily, UInt256.TryRead, TryReadTransactionState);
        }

        public IEnumerable<(UInt256 key, ImmutableArray<CoinState> coinState)> GetUnspentCoins()
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            var columnFamily = db.GetColumnFamily(UNSPENT_COIN_FAMILY);
            return db.Iterate<UInt256, ImmutableArray<CoinState>>(columnFamily, UInt256.TryRead, TryReadUnspentCoinState);
        }

        public IEnumerable<(EncodedPublicKey key, Validator validatorState)> GetValidators()
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            var columnFamily = db.GetColumnFamily(VALIDATOR_FAMILY);
            return db.Iterate<EncodedPublicKey, Validator>(columnFamily, EncodedPublicKey.TryRead, TryReadValidatorState);
        }

        #region Factory functions
        private static bool TryReadStateVersion(ref BufferReader<byte> reader, byte expectedVersion)
        {
            if (reader.TryPeek(out var value)
                && value == expectedVersion)
            {
                reader.Advance(1);
                return true;
            }

            return false;
        }

        public bool TryReadAccountState(ref BufferReader<byte> reader, out Account value)
        {
            if (TryReadStateVersion(ref reader, 0)
                && Account.TryRead(ref reader, out value))
            {
                Debug.Assert(reader.Length == 0);
                return true;
            }

            value = default;
            return false;
        }

        public bool TryReadAssetState(ref BufferReader<byte> reader, out Asset value)
        {
            if (TryReadStateVersion(ref reader, 0)
                && Asset.TryRead(ref reader, out value))
            {
                Debug.Assert(reader.Length == 0);
                return true;
            }

            value = default;
            return false;
        }

        public bool TryReadBlockState(ref BufferReader<byte> reader, out (long systemFee, BlockHeader header, ImmutableArray<UInt256> hashes) value)
        {
            if (TryReadStateVersion(ref reader, 0)
                && reader.TryReadLittleEndian(out long systemFee)
                && BlockHeader.TryRead(ref reader, out var header)
                && reader.TryReadVarArray<UInt256>(UInt256.TryRead, out var hashes))
            {
                Debug.Assert(reader.Length == 0);
                value = (systemFee, header, hashes);
                return true;
            }

            value = default;
            return false;
        }

        public bool TryReadContractState(ref BufferReader<byte> reader, out DeployedContract value)
        {
            if (TryReadStateVersion(ref reader, 0)
                && DeployedContract.TryRead(ref reader, out value))
            {
                Debug.Assert(reader.Length == 0);
                return true;
            }

            value = default;
            return false;
        }

        public bool TryReadCurrentBlockHashState(ref BufferReader<byte> reader, out (UInt256 hash, uint index) value)
        {
            if (TryReadStateVersion(ref reader, 0)
                && UInt256.TryRead(ref reader, out var hash)
                && reader.TryReadLittleEndian(out uint index))
            {
                Debug.Assert(reader.Length == 0);
                value = (hash, index);
                return true;
            }

            value = default;
            return false;
        }

        public bool TryReadStorageItemState(ref BufferReader<byte> reader, out StorageItem value)
        {
            if (TryReadStateVersion(ref reader, 0)
                && StorageItem.TryRead(ref reader, out var item))
            {
                Debug.Assert(reader.Length == 0);
                value = item;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryReadTransactionState(ref BufferReader<byte> reader, out (uint blockIndex, Transaction tx) value)
        {
            if (TryReadStateVersion(ref reader, 0)
                && reader.TryReadLittleEndian(out uint blockIndex)
                && Transaction.TryRead(ref reader, out var tx))
            {
                Debug.Assert(reader.Length == 0);
                value = (blockIndex, tx);
                return true;
            }

            value = default;
            return false;
        }

        public bool TryReadUnspentCoinState(ref BufferReader<byte> reader, out ImmutableArray<CoinState> value)
        {
            if (TryReadStateVersion(ref reader, 0)
                && reader.TryReadVarArray(out var array))
            {
                Debug.Assert(reader.Length == 0);
                value = Unsafe.As<ImmutableArray<byte>, ImmutableArray<CoinState>>(ref array);
                return true;
            }

            value = default!;
            return false;
        }

        public bool TryReadValidatorState(ref BufferReader<byte> reader, out Validator value)
        {
            if (TryReadStateVersion(ref reader, 0)
                && Validator.TryRead(ref reader, out value))
            {
                Debug.Assert(reader.Length == 0);
                return true;
            }

            value = default;
            return false;
        }
        #endregion
    }
}
