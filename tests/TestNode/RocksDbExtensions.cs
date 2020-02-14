using System;
using RocksDbSharp;

namespace NeoFx.TestNode
{
    static class RocksDbExtensions
    {
        private static readonly ReadOptions defaultReadOptions = new ReadOptions();

        public static unsafe void Put(this WriteBatch batch, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value, ColumnFamilyHandle columnFamily)
        {
            fixed (byte* keyPtr = key, valuePtr = value)
            {
                batch.Put(keyPtr, (ulong)key.Length, valuePtr, (ulong)value.Length, columnFamily);
            }
        }

        public static unsafe bool KeyExists(this RocksDb db, ReadOnlySpan<byte> key, ColumnFamilyHandle columnFamily)
        {
            fixed (byte* keyPtr = key)
            {
                var pinnableSlice = Native.Instance.rocksdb_get_pinned_cf(db.Handle, defaultReadOptions.Handle,
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
    }
}
