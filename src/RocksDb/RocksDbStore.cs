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
            //if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            //static IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> EnumerateStorage(RocksDb db, UInt160 scriptHash)
            //{
            //    var keyPrefix = new byte[UInt160.Size];
            //    scriptHash.TryWrite(keyPrefix);

            //    using var iterator = db.NewIterator(db.GetColumnFamily(STORAGE_FAMILY));
            //    iterator.Seek(keyPrefix);
            //    while (iterator.Valid())
            //    {
            //        var keyReadResult = BinaryFormat.TryReadBytes(iterator.Key(), out StorageKey key);
            //        var valueReadResult = TryReadStorageItem(iterator.Value(), out var value);

            //        Debug.Assert(keyReadResult);
            //        Debug.Assert(valueReadResult);

            //        yield return (key.Key, value);
            //        iterator.Next();
            //    }
            //}

            //return EnumerateStorage(db, scriptHash);

            return null!;
        }

        public bool TryGetAccount(in UInt160 key, out Account value)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            var columnFamily = db.GetColumnFamily(ACCOUNT_FAMILY);
            Span<byte> keybuffer = stackalloc byte[UInt160.Size];

            //Span<UInt256> keySpan = stackalloc UInt256[] { key };
            //Span<byte> keyBuffer = MemoryMarshal.AsBytes(keySpan).Slice(0, UInt256.Size);

            if (key.TryWrite(keybuffer)
                && db.TryGet<Account, AccountStateReader>(keybuffer, columnFamily, out Account account))
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

            if (key.TryWrite(keybuffer)
                && db.TryGet<Asset, AssetStateReader>(keybuffer, columnFamily, out Asset asset))
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
                && db.TryGet<(long systemFee, BlockHeader header, ImmutableArray<UInt256> hashes), BlockStateReader>(keybuffer, columnFamily, out var value))
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
                && db.TryGet<DeployedContract, ContractStateReader>(keybuffer, columnFamily, out DeployedContract contract))
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

            if (db.TryGet<(UInt256 hash, uint index), HashIndexStateReader>(keybuffer, columnFamily, out var currentBlock))
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

            //var columnFamily = db.GetColumnFamily(STORAGE_FAMILY);
            //Span<byte> keybuffer = stackalloc byte[key.GetSize()];

            //if (key.TryWrite(keybuffer, out var _)
            //    && db.TryGet(keybuffer, columnFamily, TryReadStorageItem, out StorageItem item))
            //{
            //    value = item;
            //    return true;
            //}

            value = default;
            return false;
        }

        public bool TryGetTransaction(in UInt256 key, out uint index, [NotNullWhen(true)] out Transaction? value)
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            var columnFamily = db.GetColumnFamily(TX_FAMILY);
            Span<byte> keybuffer = stackalloc byte[UInt256.Size];

            if (key.TryWrite(keybuffer)
                && db.TryGet<(uint blockIndex, Transaction tx), TransactionStateReader>(keybuffer, columnFamily, out var txState))
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
                && db.TryGet<ImmutableArray<CoinState>, UnspentCoinsStateReader>(keybuffer, columnFamily, out var item))
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
            if (db.TryGet<Validator, ValidatorStateReader>(key.Key.AsSpan(), columnFamily, out Validator validator))
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
            //return db.Iterate<UInt160, Account>(columnFamily, UInt160.TryRead, TryReadAccountState);
            return null!;
        }

        public IEnumerable<(UInt256 key, Asset assetState)> GetAssets()
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            var columnFamily = db.GetColumnFamily(ASSET_FAMILY);
            //return db.Iterate<UInt256, Asset>(columnFamily, UInt256.TryRead, TryReadAssetState);
            return null!;
        }

        public IEnumerable<(UInt256 key, (long systemFee, BlockHeader header, ReadOnlyMemory<UInt256> hashes) blockState)> GetBlocks()
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            var columnFamily = db.GetColumnFamily(BLOCK_FAMILY);
            //return db.Iterate<UInt256, (long, BlockHeader, ReadOnlyMemory<UInt256>)>(columnFamily, UInt256.TryRead, TryReadBlockState);
            return null!;
        }

        public IEnumerable<(UInt160 key, DeployedContract contractState)> GetContracts()
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            var columnFamily = db.GetColumnFamily(CONTRACT_FAMILY);
            //return db.Iterate<UInt160, DeployedContract>(columnFamily, UInt160.TryRead, TryReadContractState);
            return null!;
        }

        public IEnumerable<(StorageKey key, StorageItem contractState)> GetStorages()
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            var columnFamily = db.GetColumnFamily(STORAGE_FAMILY);
            //return db.Iterate<StorageKey, StorageItem>(columnFamily, BinaryFormat.TryReadBytes, TryReadStorageItem);
            return null!;
        }

        public IEnumerable<(UInt256 key, (uint blockIndex, Transaction tx) transactionState)> GetTransactions()
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            var columnFamily = db.GetColumnFamily(TX_FAMILY);
            //return db.Iterate<UInt256, (uint, Transaction)>(columnFamily, UInt256.TryRead, TryReadTransactionState);
            return null!;
        }

        public IEnumerable<(UInt256 key, CoinState[] coinState)> GetUnspentCoins()
        {
            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            var columnFamily = db.GetColumnFamily(UNSPENT_COIN_FAMILY);
            //return db.Iterate<UInt256, CoinState[]>(columnFamily, UInt256.TryRead, TryReadUnspentCoinsState);
            return null!;
        }

        public IEnumerable<(EncodedPublicKey key, Validator validatorState)> GetValidators()
        {
            //static bool TryReadEncodedPublicKey(ReadOnlySpan<byte> span, out EncodedPublicKey value)
            //{
            //    var reader = new SpanReader<byte>(span);
            //    return BinaryFormat.TryRead(ref reader, out value);
            //}

            if (objectDisposed) { throw new ObjectDisposedException(nameof(RocksDbStore)); }

            var columnFamily = db.GetColumnFamily(VALIDATOR_FAMILY);
            //return db.Iterate<EncodedPublicKey, Validator>(columnFamily, TryReadEncodedPublicKey, TryReadValidatorState);
            return null!;
        }

        #region Converter Structs
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

        private readonly struct AccountStateReader : ISpanReader<Account>
        {
            public bool TryReadSpan(ReadOnlySpan<byte> span, out Account value)
            {
                var reader = new BufferReader<byte>(span);
                if (TryReadStateVersion(ref reader, 0)
                    && Account.TryRead(ref reader, out value))
                {
                    Debug.Assert(reader.Length == 0);
                    return true;
                }

                value = default;
                return false;
            }
        }

        private readonly struct AssetStateReader : ISpanReader<Asset>
        {
            public bool TryReadSpan(ReadOnlySpan<byte> span, out Asset value)
            {
                var reader = new BufferReader<byte>(span);

                if (TryReadStateVersion(ref reader, 0)
                    && Asset.TryRead(ref reader, out value))
                {
                    Debug.Assert(reader.Length == 0);
                    return true;
                }

                value = default;
                return false;
            }
        }


        private readonly struct BlockStateReader : ISpanReader<(long systemFee, BlockHeader header, ImmutableArray<UInt256> hashes)>
        {
            public bool TryReadSpan(ReadOnlySpan<byte> span, out (long systemFee, BlockHeader header, ImmutableArray<UInt256> hashes) value)
            {
                var reader = new BufferReader<byte>(span);

                if (TryReadStateVersion(ref reader, 0)
                    && reader.TryReadLittleEndian(out long systemFee)
                    && BlockHeader.TryRead(ref reader, out var header)
                    && reader.TryReadVarArray<UInt256, UInt256.Factory>(out var hashes))
                {
                    Debug.Assert(reader.Length == 0);
                    value = (systemFee, header, hashes);
                    return true;
                }

                value = default;
                return false;
            }
        }


        private readonly struct ContractStateReader : ISpanReader<DeployedContract>
        {
            public bool TryReadSpan(ReadOnlySpan<byte> span, out DeployedContract value)
            {
                var reader = new BufferReader<byte>(span);

                if (TryReadStateVersion(ref reader, 0)
                    && DeployedContract.TryRead(ref reader, out value))
                {
                    Debug.Assert(reader.Length == 0);
                    return true;
                }

                value = default;
                return false;
            }
        }

        private readonly struct HashIndexStateReader : ISpanReader<(UInt256 hash, uint index)>
        {
            public bool TryReadSpan(ReadOnlySpan<byte> span, out (UInt256 hash, uint index) value)
            {
                var reader = new BufferReader<byte>(span);

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
        }

        private readonly struct StorageItemReader : ISpanReader<StorageItem>
        {
            public bool TryReadSpan(ReadOnlySpan<byte> span, out StorageItem value)
            {
                var reader = new BufferReader<byte>(span);

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
        }

        private readonly struct TransactionStateReader : ISpanReader<(uint blockIndex, Transaction tx)>
        {
            public bool TryReadSpan(ReadOnlySpan<byte> span, out (uint blockIndex, Transaction tx) value)
            {
                var reader = new BufferReader<byte>(span);

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
        }

        private readonly struct UnspentCoinsStateReader : ISpanReader<ImmutableArray<CoinState>>
        {
            public bool TryReadSpan(ReadOnlySpan<byte> span, out ImmutableArray<CoinState> value)
            {
                var reader = new BufferReader<byte>(span);

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
        }

        private readonly struct ValidatorStateReader : ISpanReader<Validator>
        {
            public bool TryReadSpan(ReadOnlySpan<byte> span, out Validator value)
            {
                var reader = new BufferReader<byte>(span);

                if (TryReadStateVersion(ref reader, 0)
                    && Validator.TryRead(ref reader, out value))
                {
                    Debug.Assert(reader.Length == 0);
                    return true;
                }

                value = default;
                return false;
            }
        }
        #endregion
    }
}
