using System;
using System.Buffers;
using DevHawk.Buffers;
using NeoFx;
using NeoFx.Models;
using NeoFx.Storage;
using RocksDbSharp;

namespace NeoFx.TestNode
{
    public sealed class RocksDbHeaderStorage : IDisposable, IHeaderStorage
    {
        private static ReadOptions DefaultReadOptions { get; } = new ReadOptions();

        private readonly RocksDb db;
        private readonly ColumnFamilyHandle headersFamily;
        private readonly ColumnFamilyHandle headerIndexFamily;

        public const string HEADERS_FAMILY = "data:headers";
        public const string HEADER_INDEX_FAMILY = "ix:header_index";

        private static ColumnFamilies ColumnFamilies => new ColumnFamilies {
            { HEADERS_FAMILY, new ColumnFamilyOptions() },
            { HEADER_INDEX_FAMILY, new ColumnFamilyOptions() },
        };

        public RocksDbHeaderStorage(string path)
        {
            var options = new DbOptions()
                .SetCreateIfMissing(true)
                .SetCreateMissingColumnFamilies(true);

            db = RocksDb.Open(options, path, ColumnFamilies);
            headersFamily = db.GetColumnFamily(HEADERS_FAMILY);
            headerIndexFamily = db.GetColumnFamily(HEADER_INDEX_FAMILY);
        }

        public void Dispose()
        {
            db.Dispose();
        }

        private static bool TryReadIndex(ReadOnlySpan<byte> span, out uint value)
            => System.Buffers.Binary.BinaryPrimitives.TryReadUInt32BigEndian(span, out value);

        private static void WriteIndex(Span<byte> span, uint value)
            => System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(span, value);

        public int Count
        {
            get
            {
                var count = 0;
                using var iterator = db.NewIterator(headerIndexFamily);
                iterator.SeekToFirst();
                while (iterator.Valid())
                {
                    count++;
                    iterator.Next();
                }
                return count;
            }
        }

        public unsafe void Add(in BlockHeader header)
        {
            var hash = header.CalculateHash();

            Span<byte> hashBuffer = stackalloc byte[UInt256.Size];
            hash.Write(hashBuffer);

            var headerBuffer = new ArrayBufferWriter<byte>(header.Size);
            var writer = new BufferWriter<byte>(headerBuffer);
            header.WriteTo(ref writer);
            writer.Commit();

            Span<byte> indexBuffer = stackalloc byte[sizeof(uint)];
            WriteIndex(indexBuffer, header.Index);

            fixed (byte* hashPtr = hashBuffer, headerPtr = headerBuffer.WrittenSpan, indexPtr = indexBuffer)
            {
                var writeBatch = new WriteBatch();
                writeBatch.Put(hashPtr, (ulong)hashBuffer.Length, headerPtr, (ulong)headerBuffer.WrittenSpan.Length, headersFamily);
                writeBatch.Put(indexPtr, (ulong)indexBuffer.Length, hashPtr, (ulong)hashBuffer.Length, headerIndexFamily);
                db.Write(writeBatch);
            }
        }

        private unsafe bool TryGet(ReadOnlySpan<byte> keyBuffer, out BlockHeader header)
        {
            fixed (byte* keyPtr = keyBuffer)
            {
                var pinnableSlice = Native.Instance.rocksdb_get_pinned_cf(db.Handle, DefaultReadOptions.Handle,
                    headersFamily.Handle, (IntPtr)keyPtr, (UIntPtr)keyBuffer.Length);

                try
                {
                    var valuePtr = Native.Instance.rocksdb_pinnableslice_value(pinnableSlice, out var valueLength);
                    if (valuePtr != IntPtr.Zero)
                    {
                        var valueSpan = new ReadOnlySpan<byte>((byte*)valuePtr, (int)valueLength);
                        var valueReader = new BufferReader<byte>(valueSpan);
                        return BlockHeader.TryRead(ref valueReader, out header);
                    }

                    header = default;
                    return false;
                }
                finally
                {
                    Native.Instance.rocksdb_pinnableslice_destroy(pinnableSlice);
                }
            }
        }

        public unsafe bool TryGet(uint index, out BlockHeader header)
        {
            Span<byte> indexBuffer = stackalloc byte[sizeof(uint)];
            WriteIndex(indexBuffer, index);

            fixed (byte* indexPtr = indexBuffer)
            {
                var pinnableSlice = Native.Instance.rocksdb_get_pinned_cf(db.Handle, DefaultReadOptions.Handle,
                    headerIndexFamily.Handle, (IntPtr)indexPtr, (UIntPtr)indexBuffer.Length);

                try
                {
                    var valuePtr = Native.Instance.rocksdb_pinnableslice_value(pinnableSlice, out var valueLength);
                    if (valuePtr != IntPtr.Zero)
                    {
                        var valueSpan = new ReadOnlySpan<byte>((byte*)valuePtr, (int)valueLength);
                        return TryGet(valueSpan, out header);
                    }

                    header = default;
                    return false;
                }
                finally
                {
                    Native.Instance.rocksdb_pinnableslice_destroy(pinnableSlice);
                }
            }
        }

        public bool TryGet(in UInt256 hash, out BlockHeader header)
        {
            Span<byte> hashBuffer = stackalloc byte[UInt256.Size];
            hash.Write(hashBuffer);

            return TryGet(hashBuffer, out header);
        }

        public bool TryGetLastHash(out UInt256 hash)
        {
            static unsafe bool TryGetKey(Iterator i, out uint key)
            {
                IntPtr keyPtr = Native.Instance.rocksdb_iter_key(i.Handle, out UIntPtr keyLength);
                var span = new ReadOnlySpan<byte>((byte*)keyPtr, (int)keyLength);
                return TryReadIndex(span, out key);
            }

            static unsafe bool TryGetValue(Iterator i, out UInt256 value)
            {
                IntPtr valuePtr = Native.Instance.rocksdb_iter_value(i.Handle, out UIntPtr valueLength);
                var span = new ReadOnlySpan<byte>((byte*)valuePtr, (int)valueLength);
                return UInt256.TryRead(span, out value);
            }

            using var iterator = db.NewIterator(headerIndexFamily);
            iterator.SeekToFirst();
            if (iterator.Valid() && TryGetKey(iterator, out var key))
            {
                iterator.Next();
                while (iterator.Valid() && TryGetKey(iterator, out var newKey))
                {
                    if (key + 1 != newKey)
                    {
                        return TryGetValue(iterator, out hash);
                    }
                    key = newKey;
                    iterator.Next();
                }

                iterator.SeekToLast();
                return TryGetValue(iterator, out hash);
            }

            hash = default;
            return false;
        }
    }
}
