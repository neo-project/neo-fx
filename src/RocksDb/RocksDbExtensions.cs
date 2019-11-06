using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace NeoFx.RocksDb
{
    using RocksDb = RocksDbSharp.RocksDb;
    using ReadOptions = RocksDbSharp.ReadOptions;
    using ColumnFamilyHandle = RocksDbSharp.ColumnFamilyHandle;

    public static class RocksDbExtensions
    {
        public delegate bool TryRead<T>(ReadOnlySpan<byte> span, out T key);

        private unsafe static bool TryConvert<T>(IntPtr intPtr, UIntPtr length, TryRead<T> tryReadValue,
                                                 [MaybeNullWhen(false)] out T value)
        {
            if (intPtr != IntPtr.Zero)
            {
                var span = new ReadOnlySpan<byte>((byte*)intPtr, (int)length);
                return tryReadValue(span, out value);
            }

            value = default!;
            return false;
        }

        private static RocksDbSharp.Native Instance => RocksDbSharp.Native.Instance;
        private static ReadOptions DefaultReadOptions { get; } = new ReadOptions();

        public static bool TryGet<T>(
            this RocksDb db,
            ReadOnlySpan<byte> key,
            ColumnFamilyHandle columnFamily,
            TryRead<T> tryReadValue,
            [MaybeNullWhen(false)] out T value)
        {
            return TryGet(db, key, columnFamily, null, tryReadValue, out value);
        }

        public static unsafe bool TryGet<T>(
            this RocksDb db,
            ReadOnlySpan<byte> key,
            ColumnFamilyHandle columnFamily,
            ReadOptions? readOptions,
            TryRead<T> tryReadValue,
            [MaybeNullWhen(false)] out T value)
        {
            fixed (byte* keyPtr = key)
            {
                var pinnableSlice = Instance.rocksdb_get_pinned_cf(db.Handle, (readOptions ?? DefaultReadOptions).Handle,
                    columnFamily.Handle, (IntPtr)keyPtr, (UIntPtr)key.Length);

                try
                {
                    var valuePtr = Instance.rocksdb_pinnableslice_value(pinnableSlice, out var valueLength);
                    return TryConvert(valuePtr, valueLength, tryReadValue, out value);
                }
                finally
                {
                    Instance.rocksdb_pinnableslice_destroy(pinnableSlice);
                }
            }
        }

        public static IEnumerable<(TKey key, TValue value)> Iterate<TKey, TValue>(
            this RocksDb db,
            ColumnFamilyHandle columnFamily,
            TryRead<TKey> tryReadKey,
            TryRead<TValue> tryReadValue)
        {
            using var iterator = db.NewIterator(columnFamily);
            iterator.SeekToFirst();
            while (iterator.Valid())
            {
                IntPtr keyPtr = Instance.rocksdb_iter_key(iterator.Handle, out UIntPtr keyLength);
                var keyReadResult = TryConvert(keyPtr, keyLength, tryReadKey, out var key);
                Debug.Assert(keyReadResult);

                IntPtr valuePtr = Instance.rocksdb_iter_value(iterator.Handle, out UIntPtr valueLength);
                var valueReadResult = TryConvert(valuePtr, valueLength, tryReadValue, out var value);
                Debug.Assert(valueReadResult);

                yield return (key, value);
                iterator.Next();
            }
        }
    }
}
