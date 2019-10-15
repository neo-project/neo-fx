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
            [MaybeNullWhen(false)] out TValue value,
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

                value = default!;
                return false;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(valueBuffer);
            }
        }


        public static bool TryGet<TValue>(
            this RocksDb db,
            string columnFamily,
            byte key,
            [MaybeNullWhen(false)] out TValue value,
            int valueSize,
            TryRead<TValue> tryReadValue)
        {
            var keyBuffer = ArrayPool<byte>.Shared.Rent(1);
            try
            {
                keyBuffer[0] = key;
                return db.TryGet(columnFamily, keyBuffer, 1, out value, valueSize, tryReadValue);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(keyBuffer);
            }
        }

        public static bool TryGet<TKey, TValue>(
            this RocksDb db,
            string columnFamily,
            TKey key,
            [MaybeNullWhen(false)] out TValue value,
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

                value = default!;
                return false;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(keyBuffer);
            }
        }


        public static bool TryGet2<TKey>(this RocksDb db, TKey key, int keySize, TryWriteKey<TKey> tryWriteKey, byte[] value, RocksDbSharp.ColumnFamilyHandle columnFamily,  out long bytesWritten)
        {
            var keyBuffer = ArrayPool<byte>.Shared.Rent(keySize);

            try
            {
                if (tryWriteKey(key, keyBuffer.AsSpan().Slice(0, keySize)))
                {
                    return TryGet2(db, keyBuffer, keySize, value, columnFamily, out bytesWritten);
                }

                bytesWritten = 0;
                return false;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(keyBuffer);
            }
        }

        public static bool TryGet2(this RocksDb db, byte[] key, long keyLength, byte[] value, RocksDbSharp.ColumnFamilyHandle columnFamily, out long bytesWritten)
        {
            var readOptions = new RocksDbSharp.ReadOptions();
            RocksDbSharp.Native.Instance.rocksdb_get_cf(db.Handle, readOptions.Handle, columnFamily.Handle,
                key, (System.UIntPtr)keyLength, out var valueLength);
            bytesWritten = db.Get(key, keyLength, value, 0, value.Length, columnFamily);
            if (bytesWritten >= 0)
            {
                Debug.Assert(bytesWritten < value.LongLength);
                return true;

            }

            return false;
        }

        public delegate (byte[] keyBuffer, int keySize) WriteKey3<TKey>(in TKey key, ArrayPool<byte> pool);

        //public static bool TrySet3<TKey, TValue>(
        //    this RocksDb db,
        //    TKey key,
        //    RocksDbSharp.ColumnFamilyHandle columnFamily,
        //    WriteKey3<TKey> writeKey,
        //    TryRead3<TValue> tryReadValue,
        //    [MaybeNullWhen(false)] out TValue value)
        //{
        //    var (keyBuffer, keySize) = writeKey(key, ArrayPool<byte>.Shared);

        //    try
        //    {
        //        return TryGet3(db, keyBuffer, keySize, columnFamily, tryReadValue, out value);
        //    }
        //    finally
        //    {
        //        ArrayPool<byte>.Shared.Return(keyBuffer);
        //    }
        //}

public delegate bool TryReadFromSpan<TValue>(ReadOnlySpan<byte> span, out TValue value);

public static unsafe bool TryGet3<T>(
    this RocksDb db,
    ReadOnlySpan<byte> key,
    RocksDbSharp.ColumnFamilyHandle columnFamily,
    TryReadFromSpan<T> tryReadValue,
    [MaybeNullWhen(false)] out T value)
{
    var instance = RocksDbSharp.Native.Instance;
    var readOptions = new RocksDbSharp.ReadOptions();

    fixed (byte* pKey = key)
    {
        var pinnableSlice = instance.rocksdb_get_pinned_cf(db.Handle, readOptions.Handle, columnFamily.Handle, (IntPtr)pKey, (UIntPtr)key.Length);
        var result = instance.rocksdb_pinnableslice_value(pinnableSlice, out var valueLength);

        try
        {
            if (result != IntPtr.Zero)
            {
                var span = new ReadOnlySpan<byte>((byte*)result, (int)valueLength);
                return tryReadValue(span, out value);
            }

            value = default!;
            return false;
        }
        finally
        {
            instance.rocksdb_pinnableslice_destroy(pinnableSlice);
        }
    }
}
    }
}
