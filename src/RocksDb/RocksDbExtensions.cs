using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace NeoFx.RocksDb
{
    using RocksDb = RocksDbSharp.RocksDb;
    using ReadOptions = RocksDbSharp.ReadOptions;
    using ColumnFamilyHandle = RocksDbSharp.ColumnFamilyHandle;
    using static RocksDbSharp.Native;

    public interface ISpanReader<T>
    {
        bool TryReadSpan(ReadOnlySpan<byte> span, out T value);
    }

    public static class RocksDbExtensions
    {
        private static ReadOptions DefaultReadOptions { get; } = new ReadOptions();

        private unsafe static bool TryConvert<T, TFactory>(IntPtr intPtr,
                                                           UIntPtr length,
                                                           TFactory factory,
                                                           [MaybeNullWhen(false)] out T value)
            where TFactory : ISpanReader<T>
        {
            if (intPtr != IntPtr.Zero)
            {
                var span = new ReadOnlySpan<byte>((byte*)intPtr, (int)length);
                return factory.TryReadSpan(span, out value);
            }

            value = default!;
            return false;
        }

        public static unsafe bool TryGet<T, TFactory>(this RocksDb db,
                                                      ReadOnlySpan<byte> key,
                                                      ColumnFamilyHandle columnFamily,
                                                      [MaybeNullWhen(false)] out T value)
            where TFactory : struct, ISpanReader<T>
        {
            return TryGet<T, TFactory>(db, key, columnFamily, null, default, out value);
        }

        public static unsafe bool TryGet<T, TFactory>(this RocksDb db,
                                                      ReadOnlySpan<byte> key,
                                                      ColumnFamilyHandle columnFamily,
                                                      TFactory factory,
                                                      [MaybeNullWhen(false)] out T value)
            where TFactory : ISpanReader<T>
        {
            return TryGet(db, key, columnFamily, null, factory, out value);
        }

        public static unsafe bool TryGet<T, TFactory>(this RocksDb db,
                                                      ReadOnlySpan<byte> key,
                                                      ColumnFamilyHandle columnFamily,
                                                      ReadOptions? readOptions,
                                                      [MaybeNullWhen(false)] out T value)
            where TFactory : struct, ISpanReader<T>
        {
            return TryGet<T, TFactory>(db, key, columnFamily, readOptions, default, out value);
        }

        public static unsafe bool TryGet<T, TFactory>(this RocksDb db,
                                                      ReadOnlySpan<byte> key,
                                                      ColumnFamilyHandle columnFamily,
                                                      ReadOptions? readOptions,
                                                      TFactory factory,
                                                      [MaybeNullWhen(false)] out T value)
            where TFactory : ISpanReader<T>
        {
            fixed (byte* keyPtr = key)
            {
                var pinnableSlice = Instance.rocksdb_get_pinned_cf(db.Handle, (readOptions ?? DefaultReadOptions).Handle,
                    columnFamily.Handle, (IntPtr)keyPtr, (UIntPtr)key.Length);

                try
                {
                    var valuePtr = Instance.rocksdb_pinnableslice_value(pinnableSlice, out var valueLength);
                    return TryConvert(valuePtr, valueLength, factory, out value);
                }
                finally
                {
                    Instance.rocksdb_pinnableslice_destroy(pinnableSlice);
                }
            }
        }

        private static IEnumerable<(TKey key, TValue value)> Iterate<TKey, TKeyFactory, TValue, TValueFactory>(RocksDbSharp.Iterator iterator,
                                                                                                               TKeyFactory keyFactory,
                                                                                                               TValueFactory valueFactory)
            where TKeyFactory : ISpanReader<TKey>
            where TValueFactory : ISpanReader<TValue>
        {
            while (iterator.Valid())
            {
                IntPtr keyPtr = Instance.rocksdb_iter_key(iterator.Handle, out UIntPtr keyLength);
                var keyReadResult = TryConvert<TKey, TKeyFactory>(keyPtr, keyLength, keyFactory, out var key);
                Debug.Assert(keyReadResult);

                IntPtr valuePtr = Instance.rocksdb_iter_value(iterator.Handle, out UIntPtr valueLength);
                var valueReadResult = TryConvert<TValue, TValueFactory>(valuePtr, valueLength, valueFactory, out var value);
                Debug.Assert(valueReadResult);

                yield return (key, value);
                iterator.Next();
            }
        }

        public static IEnumerable<(TKey key, TValue value)> Iterate<TKey, TKeyFactory, TValue, TValueFactory>(this RocksDb db,
                                                                                                              ColumnFamilyHandle columnFamily)
            where TKeyFactory : struct, ISpanReader<TKey>
            where TValueFactory : struct, ISpanReader<TValue>
        {
            return Iterate<TKey, TKeyFactory, TValue, TValueFactory>(db, columnFamily, default, default);
        }

        public static IEnumerable<(TKey key, TValue value)> Iterate<TKey, TKeyFactory, TValue, TValueFactory>(this RocksDb db,
                                                                                                              ColumnFamilyHandle columnFamily,
                                                                                                              TKeyFactory keyFactory,
                                                                                                              TValueFactory valueFactory)
            where TKeyFactory : ISpanReader<TKey>
            where TValueFactory : ISpanReader<TValue>
        {
            using var iterator = db.NewIterator(columnFamily);
            iterator.SeekToFirst();
            return Iterate<TKey, TKeyFactory, TValue, TValueFactory>(iterator, keyFactory, valueFactory);
        }

        public static unsafe IEnumerable<(TKey key, TValue value)> Search<TKey, TKeyFactory, TValue, TValueFactory>(this RocksDb db,
                                                                                                                    ColumnFamilyHandle columnFamily,
                                                                                                                    Span<byte> prefix)
            where TKeyFactory : struct, ISpanReader<TKey>
            where TValueFactory : struct, ISpanReader<TValue>
        {
            return Search<TKey, TKeyFactory, TValue, TValueFactory>(db, columnFamily, prefix, default, default);
        }

        public static unsafe IEnumerable<(TKey key, TValue value)> Search<TKey, TKeyFactory, TValue, TValueFactory>(this RocksDb db,
                                                                                                                    ColumnFamilyHandle columnFamily,
                                                                                                                    Span<byte> prefix,
                                                                                                                    TKeyFactory keyFactory,
                                                                                                                    TValueFactory valueFactory)
            where TKeyFactory : ISpanReader<TKey>
            where TValueFactory : ISpanReader<TValue>
        {
            fixed (byte* prefixPtr = prefix)
            {
                using var iterator = db.NewIterator(columnFamily);
                iterator.Seek(prefixPtr, (ulong)prefix.Length);
                return Iterate<TKey, TKeyFactory, TValue, TValueFactory>(iterator, keyFactory, valueFactory);
            }
        }
    }
}