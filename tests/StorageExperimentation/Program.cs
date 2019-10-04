using NeoFx;
using NeoFx.Models;
using NeoFx.RocksDb;
using NeoFx.Storage;
using RocksDbSharp;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace StorageExperimentation
{
    internal static class Extensions
    { 
        public static T Deserialize<T>(this byte[] array) where T : Neo.IO.ISerializable, new()
        {
            return Neo.IO.Helper.AsSerializable<T>(array);
        }
    }

    internal static class Program
    {

        private static void Main()
        {
            var cpArchivePath = @"C:\Users\harry\Source\neo\seattle\express\src\neo-express\cp2.neo-express-checkpoint";
                //Path.GetFullPath("./cp1.neo-express-checkpoint");

            if (!File.Exists(cpArchivePath))
            {
                throw new Exception("Can't find checkpoint archive");
            }

            var murmur = Murmur.MurmurHash.Create32();
            var hashArray = murmur.ComputeHash(System.Text.Encoding.UTF8.GetBytes(cpArchivePath));
            var hash = BitConverter.ToUInt32(hashArray);

            Console.WriteLine($"{cpArchivePath} {hash}");

            var cpTempPath = Path.Combine(Path.GetTempPath(), $"NeoFX.StorageExperimentation.{hash}");
            if (Directory.Exists(cpTempPath))
            {
                Directory.Delete(cpTempPath, true);
            }

            System.IO.Compression.ZipFile.ExtractToDirectory(cpArchivePath, cpTempPath);
            Console.WriteLine(cpTempPath);

            RocksDbStoreTryGetBlockExperiment(cpTempPath);
        }

        static void RunWithDB(string path, Action<RocksDb> action)
        {
            var options = new DbOptions()
                .SetCreateIfMissing(false)
                .SetCreateMissingColumnFamilies(false);

            using var db = RocksDb.Open(options, path, ColumnFamilies);
            action(db);
        }

        private static void RocksDbStoreTryGetBlockExperiment(string path)
        {
            using var storage = new RocksDbStore(path);
            if (storage.TryGetBlock(0, out var block))
            {
                foreach (var tx in block.Transactions.Span)
                {
                    if (tx.Type == TransactionType.Register
                        && RegisterTransactionData.TryRead(tx.TransactionData, out var data))
                    {
                        Console.WriteLine(data.AssetType);
                        Console.WriteLine(data.Name);
                    }
                }
            }
        }


        #region RocksDb constants
        public const string BLOCK_FAMILY = "data:block";
        public const string TX_FAMILY = "data:transaction";
        public const string ACCOUNT_FAMILY = "st:account";
        public const string ASSET_FAMILY = "st:asset";
        public const string CONTRACT_FAMILY = "st:contract";
        public const string HEADER_HASH_LIST_FAMILY = "ix:header-hash-list";
        public const string SPENT_COIN_FAMILY = "st:spent-coin";
        public const string STORAGE_FAMILY = "st:storage";
        public const string UNSPENT_COIN_FAMILY = "st:coin";
        public const string VALIDATOR_FAMILY = "st:validator";
        public const string METADATA_FAMILY = "metadata";
        public const string GENERAL_STORAGE_FAMILY = "general-storage";

        public const byte VALIDATORS_COUNT_KEY = 0x90;
        public const byte CURRENT_BLOCK_KEY = 0xc0;
        public const byte CURRENT_HEADER_KEY = 0xc1;

        public static ColumnFamilies ColumnFamilies => new ColumnFamilies {
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

        private delegate bool TryRead<T>(ReadOnlyMemory<byte> span, out T key);
        private delegate bool TryWriteKey<TKey>(in TKey key, Span<byte> span);

        private static IEnumerable<(TKey key, TValue value)> Iterate<TKey, TValue>(
            RocksDb db,
            string familyName,
            TryRead<TKey> tryReadKey,
            TryRead<TValue> tryReadValue)
        {
            using var iterator = db.NewIterator(db.GetColumnFamily(familyName));
            iterator.SeekToFirst();
            while (iterator.Valid())
            {
                var keyReadResult = tryReadKey(iterator.Key(), out var key);
                var valueReadResult = tryReadValue(iterator.Value(), out var value);

                Debug.Assert(keyReadResult && iterator.Key().Length == UInt256.Size);
                Debug.Assert(valueReadResult);

                yield return (key, value);
                iterator.Next();
            }
        }

        private static bool TryGet<TKey, TValue>(
            RocksDb db,
            string columnFamily,
            TKey key,
            [MaybeNull] out TValue value,
            int keySize,
            int valueSize,
            TryWriteKey<TKey> tryWriteKey,
            TryRead<TValue> tryReadValue)
        {
            var keyBuffer = ArrayPool<byte>.Shared.Rent(keySize);
            var valueBuffer = ArrayPool<byte>.Shared.Rent(valueSize);
            try
            {
                if (tryWriteKey(key, keyBuffer.AsSpan().Slice(0, keySize)))
                {
                    var count = db.Get(keyBuffer, keySize, valueBuffer, 0, valueSize, db.GetColumnFamily(columnFamily));
                    if (count >= 0)
                    {
                        Debug.Assert(count < valueSize);
                        return tryReadValue(valueBuffer.AsMemory().Slice(0, (int)count), out value);
                    }
                }

#pragma warning disable CS8653 // A default expression introduces a null value for a type parameter.
                value = default;
#pragma warning restore CS8653 // A default expression introduces a null value for a type parameter.
                return false;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(keyBuffer);
                ArrayPool<byte>.Shared.Return(valueBuffer);
            }
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

        private static bool TryReadUInt256(ReadOnlyMemory<byte> memory, out UInt256 key)
        {
            return UInt256.TryRead(memory.Span, out key);
        }

        private static bool TryWriteUInt256(in UInt256 key, Span<byte> span)
        {
            return key.TryWriteBytes(span);
        }
    }
}
