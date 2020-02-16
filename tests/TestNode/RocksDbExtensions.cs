using System;
using System.Diagnostics.CodeAnalysis;
using RocksDbSharp;

namespace NeoFx.TestNode
{
    static class RocksDbExtensions
    {
        private static readonly ReadOptions defaultReadOptions = new ReadOptions();

        public static bool ColumnFamilyEmpty(this RocksDb db, ColumnFamilyHandle columnFamily, ReadOptions? readOptions = null)
        {
            var iter = db.NewIterator(columnFamily, readOptions);
            iter.SeekToFirst();
            return !iter.Valid();
        }

        public static unsafe void Put(this WriteBatch batch, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value, ColumnFamilyHandle columnFamily)
        {
            fixed (byte* keyPtr = key, valuePtr = value)
            {
                batch.Put(keyPtr, (ulong)key.Length, valuePtr, (ulong)value.Length, columnFamily);
            }
        }

        public static unsafe bool KeyExists(this RocksDb db, ReadOnlySpan<byte> key, ColumnFamilyHandle columnFamily, ReadOptions? readOptions = null)
        {
            readOptions = readOptions ?? defaultReadOptions;

            fixed (byte* keyPtr = key)
            {
                var pinnableSlice = Native.Instance.rocksdb_get_pinned_cf(db.Handle, readOptions.Handle,
                    columnFamily.Handle, (IntPtr)keyPtr, (UIntPtr)key.Length);

                try
                {
                    var valuePtr = Native.Instance.rocksdb_pinnableslice_value(pinnableSlice, out var valueLength);
                    if (valuePtr != IntPtr.Zero)
                    {
                        return true;
                    }

                    return false;
                }
                finally
                {
                    Native.Instance.rocksdb_pinnableslice_destroy(pinnableSlice);
                }
            }
        }

        public delegate bool TryRead<T>(ReadOnlySpan<byte> span, [MaybeNullWhen(false)] out T value);

        public static bool TryConvert<T>(IntPtr intPtr,
                                         UIntPtr length,
                                         TryRead<T> factory,
                                         [MaybeNullWhen(false)] out T value)
        {
            if (intPtr != IntPtr.Zero)
            {
                unsafe 
                {
                    var span = new ReadOnlySpan<byte>((byte*)intPtr, (int)length);
                    return factory(span, out value);
                }
            }

            value = default!;
            return false;
        }
    }
}
