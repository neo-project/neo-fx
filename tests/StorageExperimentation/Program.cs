﻿using NeoFx;
using NeoFx.Models;
using NeoFx.RocksDb;
using NeoFx.Storage;
using RocksDbSharp;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Linq;

using Console = Colorful.Console;

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
            var cpArchivePath = @"C:\Users\harry\Source\neo\seattle\express\src\neo-express\cponline.neo-express-checkpoint";
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

            RocksDBExperiment(cpTempPath);
        }

        //static void RunWithDB(string path, Action<RocksDb> action)
        //{
        //    var options = new DbOptions()
        //        .SetCreateIfMissing(false)
        //        .SetCreateMissingColumnFamilies(false);

        //    using var db = RocksDb.Open(options, path, ColumnFamilies);
        //    action(db);
        //}

        private static void RocksDBExperiment(string path)
        {
            using var store = new RocksDbStore(path);

            for (uint i = 0; i < store.Height; i++)
            {
                if (store.TryGetBlockHash(i, out var blockHash)
                    && store.TryGetBlock(blockHash, out var header, out var hashes))
                {
                    for (int j = 0; j < hashes.Length; j++)
                    {
                        if (store.TryGetTransaction(hashes.Span[j], out var _, out var tx)
                            && HashHelpers.TryHash(tx, out var txHash))
                        {
                            Console.WriteLine($"{txHash.Equals(hashes.Span[j])} {txHash} {hashes.Span[j]}");
                            if (!txHash.Equals(hashes.Span[j])) throw new Exception($"block {i} tx {j}");
                        }
                        else
                        {
                            Console.WriteLine($"TX {hashes.Span[j]}", Color.Red);
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Block {i}", Color.Yellow);
                }
            }

            ;
        }
        private static void RocksDBExperimentxx(string path)
        {
            var options = new DbOptions()
                .SetCreateIfMissing(false)
                .SetCreateMissingColumnFamilies(false);

            using var db = RocksDb.Open(options, path, ColumnFamilies);

            var blockIndex = GetBlocks(db)
                .OrderBy(t => t.blockState.header.Index)
                .Select(t => t.key)
                .ToList();

            Span<byte> keyBuffer = stackalloc byte[UInt256.Size];
            if (blockIndex[0].TryWrite(keyBuffer)
                && db.TryGet<(long, BlockHeader, ReadOnlyMemory<UInt256>)>(keyBuffer, db.GetColumnFamily(BLOCK_FAMILY), TryReadBlockStateSpan, out var value))
            {
                Console.WriteLine(value.Item1);
            }
            else
            {
                throw new Exception();

            }

            ;
        }

        private static bool TryReadBlockStateSpan(ReadOnlySpan<byte> span, out (long systemFee, BlockHeader header, ReadOnlyMemory<UInt256> hashes) value)
        {
            return TryReadBlockState(span.ToArray().AsMemory(), out value);
        }

        private static IEnumerable<(UInt256 key, (long systemFee, BlockHeader header, ReadOnlyMemory<UInt256> hashes) blockState)> GetBlocks(RocksDb db)
        {
            return null!;
            //return db.Iterate<UInt256, (long, BlockHeader, ReadOnlyMemory<UInt256>)>(BLOCK_FAMILY, TryReadUInt256Key, TryReadBlockState);
        }

        private static bool TryReadBlockState(ReadOnlyMemory<byte> memory, out (long systemFee, BlockHeader header, ReadOnlyMemory<UInt256> hashes) value)
        {
            //var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(memory));

            //if (TryReadStateVersion(ref reader, 0)
            //    && reader.TryRead(out long systemFee)
            //    && reader.TryRead(out BlockHeader header)
            //    && reader.TryReadVarArray<UInt256>(BinaryFormat.TryRead, out var hashes))
            //{
            //    Debug.Assert(reader.Remaining == 0);
            //    value = (systemFee, header, hashes);
            //    return true;
            //}

            value = default;
            return false;
        }

        private static bool TryReadUInt256Key(ReadOnlyMemory<byte> memory, out UInt256 key)
        {
            Debug.Assert(memory.Length == UInt256.Size);
            return UInt256.TryRead(memory.Span, out key);
        }


        private static void RocksDbStoreTryGetBlockExperiment(string path)
        {
            using var storage = new RocksDbStore(path);

            Console.WriteLine($"NEO: {storage.GoverningTokenHash}");
            Console.WriteLine($"GAS: {storage.UtilityTokenHash}");

            int max = 0;

            for (uint i = 0; i < storage.Height; i++)
            {
                if (storage.TryGetBlock(i, out var block))
                {
                    for (int j = 0; j < block.Transactions.Length; j++)
                    {
                        max = Math.Max(max, block.Transactions.Span[j].GetSize());
                    }
                    //Console.WriteLine($"{i}\t\t{block.Timestamp}");
                    //for (var j = 0; j < block.Transactions.Length; j++)
                    //{
                    //    Console.WriteLine($"  {block.Transactions.Span[j].Type}");
                    //}
                }
                else
                {
                    Console.WriteLine($"TryGetBlock {i} failed", Color.Red);
                }
            }

            Console.WriteLine($"{max}", Color.Cyan);

            //if (storage.TryGetBlockHash(0, out var genesisHash)
            //    && storage.TryGetBlock(genesisHash, out BlockHeader _, out var hashes))
            //{
            //    for (int i = 0; i < hashes.Length; i++)
            //    {
            //        if (storage.TryGetTransaction(hashes.Span[i], out var _, out var tx))
            //        {
            //            Console.WriteLine(tx.GetType().FullName);
            //        }

            //        //    && HashHelpers.TryHash(tx, out var newhash))
            //        //{
            //        //    Console.WriteLine(tx.Type);
            //        //    Console.WriteLine(hashes.Span[i]);
            //        //    Console.WriteLine(newhash);
            //        //}
            //    }
            //}




            //for (uint i = 0; i < storage.Height; i++)
            //{
            //    if (storage.TryGetBlock(i, out var block))
            //    {
            //        Console.WriteLine($"{i}\t\t{block.Timestamp}");
            //        for (var j = 0; j < block.Transactions.Length; j++)
            //        {
            //            Console.WriteLine($"  {block.Transactions.Span[j].Type}");
            //        }
            //    }
            //}

            //if (storage.TryGetBlock(0, out var block))
            //{
            //    foreach (var tx in block.Transactions.Span)
            //    {
            //        //if (tx.Type == TransactionType.Register
            //        //    && RegisterTransactionData.TryRead(tx.TransactionData, out var data))
            //        //{
            //        //    Console.WriteLine(data.AssetType);
            //        //    Console.WriteLine(data.Name);
            //        //}
            //    }
            //}
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
            [MaybeNullWhen(false)] out TValue value,
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

                value = default!;
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
            return key.TryWrite(span);
        }
    }
}
