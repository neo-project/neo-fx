using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace NeoFx.RocksDb
{
    using RocksDb = RocksDbSharp.RocksDb;
    using ReadOptions = RocksDbSharp.ReadOptions;
    using ColumnFamilyHandle = RocksDbSharp.ColumnFamilyHandle;
    using WriteBatch = RocksDbSharp.WriteBatch;
    using static RocksDbSharp.Native;

    public interface ISpanReader<T>
    {
        bool TryReadSpan(ReadOnlySpan<byte> span, out T value);
    }

    public interface ISpanWriter<T>
    {
        int GetSize(in T value);
        int Write(in T value, Span<byte> span);
    }

    public static class RocksDbExtensions
    {
        private static ReadOptions DefaultReadOptions { get; } = new ReadOptions();

        private unsafe static bool TryConvert<T, TReader>(IntPtr intPtr,
                                                          UIntPtr length,
                                                          TReader factory,
                                                          [MaybeNullWhen(false)] out T value)
            where TReader : ISpanReader<T>
        {
            if (intPtr != IntPtr.Zero)
            {
                var span = new ReadOnlySpan<byte>((byte*)intPtr, (int)length);
                return factory.TryReadSpan(span, out value);
            }

            value = default!;
            return false;
        }

        public static unsafe bool TryGet<T, TReader>(this RocksDb db,
                                                     ReadOnlySpan<byte> key,
                                                     ColumnFamilyHandle columnFamily,
                                                     [MaybeNullWhen(false)] out T value)
            where TReader : struct, ISpanReader<T>
        {
            return TryGet<T, TReader>(db, key, columnFamily, null, default, out value);
        }

        public static unsafe bool TryGet<T, TReader>(this RocksDb db,
                                                     ReadOnlySpan<byte> key,
                                                     ColumnFamilyHandle columnFamily,
                                                     TReader factory,
                                                     [MaybeNullWhen(false)] out T value)
            where TReader : ISpanReader<T>
        {
            return TryGet(db, key, columnFamily, null, factory, out value);
        }

        public static unsafe bool TryGet<T, TReader>(this RocksDb db,
                                                     ReadOnlySpan<byte> key,
                                                     ColumnFamilyHandle columnFamily,
                                                     ReadOptions? readOptions,
                                                     [MaybeNullWhen(false)] out T value)
            where TReader : struct, ISpanReader<T>
        {
            return TryGet<T, TReader>(db, key, columnFamily, readOptions, default, out value);
        }

        public static unsafe bool TryGet<T, TReader>(this RocksDb db,
                                                     ReadOnlySpan<byte> key,
                                                     ColumnFamilyHandle columnFamily,
                                                     ReadOptions? readOptions,
                                                     TReader factory,
                                                     [MaybeNullWhen(false)] out T value)
            where TReader : ISpanReader<T>
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

        private static IEnumerable<(TKey key, TValue value)> Iterate<TKey, TKeyReader, TValue, TValueReader>(RocksDbSharp.Iterator iterator,
                                                                                                             TKeyReader keyFactory,
                                                                                                             TValueReader valueFactory)
            where TKeyReader : ISpanReader<TKey>
            where TValueReader : ISpanReader<TValue>
        {
            while (iterator.Valid())
            {
                IntPtr keyPtr = Instance.rocksdb_iter_key(iterator.Handle, out UIntPtr keyLength);
                var keyReadResult = TryConvert<TKey, TKeyReader>(keyPtr, keyLength, keyFactory, out var key);
                Debug.Assert(keyReadResult);

                IntPtr valuePtr = Instance.rocksdb_iter_value(iterator.Handle, out UIntPtr valueLength);
                var valueReadResult = TryConvert<TValue, TValueReader>(valuePtr, valueLength, valueFactory, out var value);
                Debug.Assert(valueReadResult);

                yield return (key, value);
                iterator.Next();
            }
        }

        public static IEnumerable<(TKey key, TValue value)> Iterate<TKey, TKeyReader, TValue, TValueReader>(this RocksDb db,
                                                                                                            ColumnFamilyHandle columnFamily)
            where TKeyReader : struct, ISpanReader<TKey>
            where TValueReader : struct, ISpanReader<TValue>
        {
            return Iterate<TKey, TKeyReader, TValue, TValueReader>(db, columnFamily, default, default);
        }

        public static IEnumerable<(TKey key, TValue value)> Iterate<TKey, TKeyReader, TValue, TValueReader>(this RocksDb db,
                                                                                                            ColumnFamilyHandle columnFamily,
                                                                                                            TKeyReader keyFactory,
                                                                                                            TValueReader valueFactory)
            where TKeyReader : ISpanReader<TKey>
            where TValueReader : ISpanReader<TValue>
        {
            using var iterator = db.NewIterator(columnFamily);
            iterator.SeekToFirst();
            return Iterate<TKey, TKeyReader, TValue, TValueReader>(iterator, keyFactory, valueFactory);
        }

        public static unsafe IEnumerable<(TKey key, TValue value)> Search<TKey, TKeyReader, TValue, TValueReader>(this RocksDb db,
                                                                                                                  ColumnFamilyHandle columnFamily,
                                                                                                                  Span<byte> prefix)
            where TKeyReader : struct, ISpanReader<TKey>
            where TValueReader : struct, ISpanReader<TValue>
        {
            return Search<TKey, TKeyReader, TValue, TValueReader>(db, columnFamily, prefix, default, default);
        }

        public static unsafe IEnumerable<(TKey key, TValue value)> Search<TKey, TKeyReader, TValue, TValueReader>(this RocksDb db,
                                                                                                                  ColumnFamilyHandle columnFamily,
                                                                                                                  Span<byte> prefix,
                                                                                                                  TKeyReader keyFactory,
                                                                                                                  TValueReader valueFactory)
            where TKeyReader : ISpanReader<TKey>
            where TValueReader : ISpanReader<TValue>
        {
            fixed (byte* prefixPtr = prefix)
            {
                using var iterator = db.NewIterator(columnFamily);
                iterator.Seek(prefixPtr, (ulong)prefix.Length);
                return Iterate<TKey, TKeyReader, TValue, TValueReader>(iterator, keyFactory, valueFactory);
            }
        }

        public static unsafe void Put(this WriteBatch batch, ColumnFamilyHandle columnFamily, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
        {
            fixed (byte* keyPtr = key, valuePtr = value)
            {
                batch.Put(keyPtr, (ulong)key.Length, valuePtr, (ulong)value.Length, columnFamily);
            }
        }

        public static void Put<TKey, TKeyWriter, TValue, TValueWriter>(this WriteBatch batch, ColumnFamilyHandle columnFamily, in TKey key, in TValue value)
            where TKeyWriter : struct, ISpanWriter<TKey>
            where TValueWriter : struct, ISpanWriter<TValue>
        {
            Put<TKey, TKeyWriter, TValue, TValueWriter>(batch, columnFamily, key, value, default, default);
        }

        public static void Put<TKey, TKeyWriter, TValue, TValueWriter>(this WriteBatch batch, ColumnFamilyHandle columnFamily, in TKey key, in TValue value,
                TKeyWriter keyWriter, TValueWriter valueWriter)
            where TKeyWriter : ISpanWriter<TKey>
            where TValueWriter : ISpanWriter<TValue>
        {
            static IMemoryOwner<byte> GetMemory<T, TWriter>(in T value, TWriter writer, out int bytesWritten) where TWriter : ISpanWriter<T>
            {
                var owner = MemoryPool<byte>.Shared.Rent(writer.GetSize(value));
                bytesWritten = writer.Write(value, owner.Memory.Span);
                return owner;
            }

            using var keyOwner = GetMemory(key, keyWriter, out var keyWritten);
            using var valueOwner = GetMemory(value, valueWriter, out var valueWritten);
            Put(batch, columnFamily, keyOwner.Memory.Span.Slice(0, keyWritten), valueOwner.Memory.Span.Slice(0,valueWritten));
        }
    }
}