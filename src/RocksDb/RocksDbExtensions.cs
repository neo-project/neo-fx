using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using DevHawk.Buffers;
using NeoFx.Storage;

namespace NeoFx.RocksDb
{
    using RocksDb = RocksDbSharp.RocksDb;
    using ReadOptions = RocksDbSharp.ReadOptions;
    using ColumnFamilyHandle = RocksDbSharp.ColumnFamilyHandle;
    using WriteBatch = RocksDbSharp.WriteBatch;
    using static RocksDbSharp.Native;


    public static class RocksDbExtensions
    {
        private static ReadOptions DefaultReadOptions { get; } = new ReadOptions();


        private unsafe static bool TryConvert<T>(IntPtr intPtr,
                                                 UIntPtr length,
                                                 TryReadItem<T> factory,
                                                 [MaybeNullWhen(false)] out T value)
        {
            if (intPtr != IntPtr.Zero)
            {
                var span = new ReadOnlySpan<byte>((byte*)intPtr, (int)length);
                var reader = new BufferReader<byte>(span);
                return factory(ref reader, out value);
            }

            value = default!;
            return false;
        }

        public static unsafe bool KeyExists(this RocksDb db,  ColumnFamilyHandle columnFamily, ReadOnlySpan<byte> key)
        {
            fixed (byte* keyPtr = key)
            {
                var pinnableSlice = Instance.rocksdb_get_pinned_cf(db.Handle, DefaultReadOptions.Handle,
                    columnFamily.Handle, (IntPtr)keyPtr, (UIntPtr)key.Length);

                try
                {
                    var valuePtr = Instance.rocksdb_pinnableslice_value(pinnableSlice, out var valueLength);
                    if (valuePtr != IntPtr.Zero)
                    {
                        return true;
                    }

                    return false;
                }
                finally
                {
                    Instance.rocksdb_pinnableslice_destroy(pinnableSlice);
                }
            }
        }
      
        public static unsafe bool TryGet<T>(this RocksDb db,
                                                     ReadOnlySpan<byte> key,
                                                     ColumnFamilyHandle columnFamily,
                                                     TryReadItem<T> factory,
                                                     [MaybeNullWhen(false)] out T value,
                                                    ReadOptions? readOptions = null)
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

        private static IEnumerable<(TKey key, TValue value)> Iterate<TKey, TValue>(
            RocksDbSharp.Iterator iterator,
            TryReadItem<TKey> keyFactory,
            TryReadItem<TValue> valueFactory)
        {
            try
            {
                while (iterator.Valid())
                {
                    IntPtr keyPtr = Instance.rocksdb_iter_key(iterator.Handle, out UIntPtr keyLength);
                    var keyReadResult = TryConvert<TKey>(keyPtr, keyLength, keyFactory, out var key);
                    Debug.Assert(keyReadResult);

                    IntPtr valuePtr = Instance.rocksdb_iter_value(iterator.Handle, out UIntPtr valueLength);
                    var valueReadResult = TryConvert<TValue>(valuePtr, valueLength, valueFactory, out var value);
                    Debug.Assert(valueReadResult);

                    yield return (key, value);
                    iterator.Next();
                }
            }
            finally
            {
                iterator.Dispose();
            }
        }

        public static IEnumerable<(TKey key, TValue value)> Iterate<TKey, TValue>(
            this RocksDb db,
            ColumnFamilyHandle columnFamily,
            TryReadItem<TKey> keyFactory,
            TryReadItem<TValue> valueFactory)
        {
            var iterator = db.NewIterator(columnFamily);
            iterator.SeekToFirst();
            return Iterate<TKey, TValue>(iterator, keyFactory, valueFactory);
        }
        
        public static unsafe IEnumerable<(TKey key, TValue value)> Search<TKey, TValue>(
            this RocksDb db,
            ColumnFamilyHandle columnFamily,
            Span<byte> prefix,
            TryReadItem<TKey> keyFactory,
            TryReadItem<TValue> valueFactory)
        {
            fixed (byte* prefixPtr = prefix)
            {
                var iterator = db.NewIterator(columnFamily);
                iterator.Seek(prefixPtr, (ulong)prefix.Length);
                return Iterate<TKey, TValue>(iterator, keyFactory, valueFactory);
            }
        }

        public static unsafe void Put(this WriteBatch batch, ColumnFamilyHandle columnFamily, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
        {
            fixed (byte* keyPtr = key, valuePtr = value)
            {
                batch.Put(keyPtr, (ulong)key.Length, valuePtr, (ulong)value.Length, columnFamily);
            }
        }

        public static void Put<TKey, TValue>(this WriteBatch batch,
                                             ColumnFamilyHandle columnFamily,
                                             in TKey key,
                                             in TValue value)
            where TKey : IWritable<TKey>
            where TValue : IWritable<TValue>
        {
            const int MAX_STACKALLOC_SIZE = 1024;

            static void PutValue(WriteBatch batch,
                                 ColumnFamilyHandle columnFamily,
                                 ReadOnlySpan<byte> keySpan,
                                 in TValue value,
                                 Span<byte> valueSpan)
            {
                var valueWriter = new BufferWriter<byte>(valueSpan);
                value.WriteTo(ref valueWriter);
                Debug.Assert(valueWriter.Span.IsEmpty);
                batch.Put(columnFamily, keySpan, valueSpan);
            }

            static void PutKey(WriteBatch batch,
                               ColumnFamilyHandle columnFamily,
                               in TKey key,
                               Span<byte> keySpan,
                               in TValue value)
            {
                var keyWriter = new BufferWriter<byte>(keySpan);
                key.WriteTo(ref keyWriter);
                Debug.Assert(keyWriter.Span.IsEmpty);

                var valueSize = value.Size;
                if (valueSize <= MAX_STACKALLOC_SIZE)
                {
                    Span<byte> span = stackalloc byte[valueSize];
                    PutValue(batch, columnFamily, keySpan, value, span);
                }
                else
                {
                    using var owner = MemoryPool<byte>.Shared.Rent(valueSize);
                    var span = owner.Memory.Span.Slice(0, valueSize);
                    PutValue(batch, columnFamily, keySpan, value, span);
                }
            }

            var keySize = key.Size;
            if (keySize <= MAX_STACKALLOC_SIZE)
            {
                Span<byte> span = stackalloc byte[keySize];
                PutKey(batch, columnFamily, key, span, value);
            }
            else
            {
                using var owner = MemoryPool<byte>.Shared.Rent(keySize);
                var span = owner.Memory.Span.Slice(0, keySize);
                PutKey(batch, columnFamily, key, span, value);
            }
        }
    }
}