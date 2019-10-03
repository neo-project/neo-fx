using NeoFx.Models;
using RocksDbSharp;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace StorageExperimentation
{






    class Program
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


        static bool TryReadStateVersion(ref SequenceReader<byte> reader, byte expectedVersion)
        {
            if (reader.TryPeek(out var value) && value == expectedVersion)
            {
                reader.Advance(sizeof(byte));
                return true;
            }

            return false;
        }

        static IEnumerable<(UInt256 key, long systemFee, TrimmedBlock block)> GetBlocks(RocksDb db)
        {
            static bool TryReadBlockState(ReadOnlyMemory<byte> memory, out (long systemFee, TrimmedBlock block) value)
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

            using var iterator = db.NewIterator(db.GetColumnFamily(BLOCK_FAMILY));
            iterator.SeekToFirst();
            while (iterator.Valid())
            {
                var keyReadResult = UInt256.TryReadBytes(iterator.Key(), out var key);
                var valueReadResult = TryReadBlockState(iterator.Value(), out var value);

                Debug.Assert(keyReadResult && iterator.Key().Length == UInt256.Size);
                Debug.Assert(valueReadResult);

                yield return (key, value.systemFee, value.block);
                iterator.Next();
            }
        }

        // 0xc56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b && 0x602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7
        // these tx in the test data do not load. These are the two register Transactions in the genesis block
        static IEnumerable<(UInt256 key, uint blockIndex, Transaction tx)> GetTransactions(RocksDb db)
        {
            static bool TryReadTxState(ReadOnlyMemory<byte> memory, out (uint blockIndex, Transaction tx) value)
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

            using var iterator = db.NewIterator(db.GetColumnFamily(TX_FAMILY));
            iterator.SeekToFirst();
            while (iterator.Valid())
            {

                var keyReadResult = UInt256.TryReadBytes(iterator.Key(), out var key);
                Debug.Assert(keyReadResult && iterator.Key().Length == UInt256.Size);

                var valueReadResult = TryReadTxState(iterator.Value(), out var value);
                Debug.Assert(valueReadResult);
                yield return (key, value.blockIndex, value.tx);
                
                iterator.Next();
            }
        }

        private static void Main(string[] args)
        {
            var options = new DbOptions()
                .SetCreateIfMissing(false)
                .SetCreateMissingColumnFamilies(false);

            var path = Path.GetFullPath("./cp1");
            Console.WriteLine(path);

            using var db = RocksDb.Open(options, path, ColumnFamilies);

            var tx = GetTransactions(db).ToList();

            //var blocks = GetBlocks(db).ToList();
            //UInt256 prev = default;

            //foreach (var tuple in blocks.OrderByDescending(t => t.block.Timestamp))
            //{
            //    Debug.Assert(prev == default || prev == tuple.key);
            //    //Console.WriteLine($"{tuple.block.Header.Witness.InvocationScript.Length} {tuple.block.Header.Witness.VerificationScript.Length}");
            //    //Console.WriteLine(tuple.block.Header.Timestamp);
            //    //Console.WriteLine($"  {prev}");
            //    //Console.WriteLine($"  {tuple.key}");
            //    Console.WriteLine(tuple.block.Hashes.Length);
            //    prev = tuple.block.PreviousHash;
            //}
            //;

        }
    }
}
