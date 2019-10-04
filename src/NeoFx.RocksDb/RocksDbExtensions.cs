using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace NeoFx.RocksDb
{
    using RocksDb = RocksDbSharp.RocksDb;

    public static class RocksDbExtensions
    {
        public delegate bool TryRead<T>(ReadOnlyMemory<byte> span, out T key);
        public delegate bool TryWriteKey<TKey>(in TKey key, Span<byte> span);

        public static IEnumerable<(TKey key, TValue value)> Iterate<TKey, TValue>(
            this RocksDb db,
            string columnFamily,
            TryRead<TKey> tryReadKey,
            TryRead<TValue> tryReadValue)
        {
            using var iterator = db.NewIterator(db.GetColumnFamily(columnFamily));
            iterator.SeekToFirst();
            while (iterator.Valid())
            {
                var keyReadResult = tryReadKey(iterator.Key(), out var key);
                var valueReadResult = tryReadValue(iterator.Value(), out var value);

                Debug.Assert(keyReadResult);
                Debug.Assert(valueReadResult);

                yield return (key, value);
                iterator.Next();
            }
        }

        public static bool TryGet<TValue>(
            this RocksDb db,
            string columnFamily,
            byte[] keyBuffer,
            int keySize,
            [MaybeNull] out TValue value,
            int valueSize,
            TryRead<TValue> tryReadValue)
        {
            var valueBuffer = ArrayPool<byte>.Shared.Rent(valueSize);

            try
            {
                var count = db.Get(keyBuffer, keySize, valueBuffer, 0, valueSize, db.GetColumnFamily(columnFamily));
                if (count >= 0)
                {
                    Debug.Assert(count < valueSize);
                    return tryReadValue(valueBuffer.AsMemory().Slice(0, (int)count), out value);
                }

#pragma warning disable CS8653 // A default expression introduces a null value for a type parameter.
                value = default;
#pragma warning restore CS8653 // A default expression introduces a null value for a type parameter.
                return false;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(valueBuffer);
            }
        }


        public static bool TryGet<TKey, TValue>(
            this RocksDb db,
            string columnFamily,
            TKey key,
            [MaybeNull] out TValue value,
            int keySize,
            int valueSize,
            TryWriteKey<TKey> tryWriteKey,
            TryRead<TValue> tryReadValue)
        {
            var keyBuffer = ArrayPool<byte>.Shared.Rent(keySize);
            try
            {
                if (tryWriteKey(key, keyBuffer.AsSpan().Slice(0, keySize)))
                {
                    return db.TryGet(columnFamily, keyBuffer, keySize, out value, valueSize, tryReadValue);
                }

#pragma warning disable CS8653 // A default expression introduces a null value for a type parameter.
                value = default;
#pragma warning restore CS8653 // A default expression introduces a null value for a type parameter.
                return false;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(keyBuffer);
            }
        }
    }
}
