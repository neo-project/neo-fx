using NeoFx.Models;
using RocksDbSharp;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace StorageExperimentation
{
    internal static class Program
    {
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

        private static bool TryReadStateVersion(ref SequenceReader<byte> reader, byte expectedVersion)
        {
            if (reader.TryPeek(out var value) && value == expectedVersion)
            {
                reader.Advance(sizeof(byte));
                return true;
            }

            return false;
        }

        private delegate bool TryRead<T>(ReadOnlyMemory<byte> span, out T key);

        private static IEnumerable<(TKey key, TValue value)> Iterate<TKey, TValue>(
            RocksDb db, string familyName, TryRead<TKey> tryReadKey, TryRead<TValue> tryReadValue)
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

        private static bool TryReadUInt256(ReadOnlyMemory<byte> memory, out UInt256 key)
        {
            return UInt256.TryReadBytes(memory.Span, out key);
        }

        private static IEnumerable<(UInt256 key, (long systemFee, TrimmedBlock block) blockState)> GetBlocks(RocksDb db)
        {
            static bool TryReadValue(ReadOnlyMemory<byte> memory, out (long systemFee, TrimmedBlock block) value)
            {
                var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(memory));

                if (TryReadStateVersion(ref reader, 0)
                    && reader.TryReadInt64LittleEndian(out var systemFee)
                    && TrimmedBlock.TryRead(ref reader, out var block))
                {
                    Debug.Assert(reader.Remaining == 0);
                    value = (systemFee, block);
                    return true;
                }

                value = default;
                return false;
            }

            return Iterate<UInt256, (long, TrimmedBlock)>
                (db, BLOCK_FAMILY, TryReadUInt256, TryReadValue);
        }

        private static IEnumerable<(UInt256 key, (uint blockIndex, Transaction tx) txState)> GetTransactions(RocksDb db)
        {
            static bool TryReadValue(ReadOnlyMemory<byte> memory, out (uint blockIndex, Transaction tx) value)
            {
                var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(memory));

                if (TryReadStateVersion(ref reader, 0)
                    && reader.TryReadUInt32LittleEndian(out var blockIndex)
                    && Transaction.TryRead(ref reader, out var tx))
                {
                    Debug.Assert(reader.Remaining == 0);
                    value = (blockIndex, tx);
                    return true;
                }

                value = default;
                return false;
            }

            return Iterate<UInt256, (uint, Transaction)>
                (db, TX_FAMILY, TryReadUInt256, TryReadValue);
        }

        private static void Main(string[] args)
        {
            var options = new DbOptions()
                .SetCreateIfMissing(false)
                .SetCreateMissingColumnFamilies(false);

            var path = Path.GetFullPath("./cp1");
            Console.WriteLine(path);

            using var db = RocksDb.Open(options, path, ColumnFamilies);

            var blocks = GetBlocks(db).ToDictionary(t => t.key, t => t.blockState);
            var txs = GetTransactions(db).ToDictionary(t => t.key, t => t.txState);

            var blockIndex = blocks.ToDictionary(kvp => kvp.Value.block.Index, t => t.Key);

            ;
        }
    }
}
