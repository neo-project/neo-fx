using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NeoFx.Storage
{
    public static class BufferReaderExtensions
    {
        private static unsafe bool TryRead<T>(ref this BufferReader<byte> reader, out T value)
            where T : unmanaged
        {
            ReadOnlySpan<byte> span = reader.UnreadSpan;
            if (span.Length < sizeof(T))
            {
                return TryReadMultisegment(ref reader, out value);
            }

            value = Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(span));
            reader.Advance(sizeof(T));
            return true;
        }

        private static unsafe bool TryReadMultisegment<T>(ref BufferReader<byte> reader, out T value)
            where T : unmanaged
        {
            Debug.Assert(reader.UnreadSpan.Length < sizeof(T), "reader.UnreadSpan.Length < sizeof(T)");

            // Not enough data in the current segment, try to peek for the data we need.
            T buffer = default;
            Span<byte> tempSpan = new Span<byte>(&buffer, sizeof(T));

            if (!reader.TryCopyTo(tempSpan))
            {
                value = default;
                return false;
            }

            value = Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(tempSpan));
            reader.Advance(sizeof(T));
            return true;
        }

        public static bool TryRead(ref this BufferReader<byte> reader, out sbyte value)
        {
            if (TryRead(ref reader, out byte byteValue))
            {
                value = unchecked((sbyte)byteValue);
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryReadLittleEndian(ref this BufferReader<byte> reader, out short value)
        {
            if (BitConverter.IsLittleEndian)
            {
                return reader.TryRead(out value);
            }

            return TryReadReverseEndianness(ref reader, out value);
        }

        public static bool TryReadBigEndian(ref this BufferReader<byte> reader, out short value)
        {
            if (!BitConverter.IsLittleEndian)
            {
                return reader.TryRead(out value);
            }

            return TryReadReverseEndianness(ref reader, out value);
        }

        private static bool TryReadReverseEndianness(ref BufferReader<byte> reader, out short value)
        {
            if (reader.TryRead(out value))
            {
                value = BinaryPrimitives.ReverseEndianness(value);
                return true;
            }

            return false;
        }

        public static bool TryReadLittleEndian(ref this BufferReader<byte> reader, out ushort value)
        {
            if (TryReadLittleEndian(ref reader, out short signedvalue))
            {
                value = unchecked((ushort)signedvalue);
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryReadBigEndian(ref this BufferReader<byte> reader, out ushort value)
        {
            if (TryReadBigEndian(ref reader, out short signedvalue))
            {
                value = unchecked((ushort)signedvalue);
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryReadLittleEndian(ref this BufferReader<byte> reader, out int value)
        {
            if (BitConverter.IsLittleEndian)
            {
                return reader.TryRead(out value);
            }

            return TryReadReverseEndianness(ref reader, out value);
        }

        public static bool TryReadBigEndian(ref this BufferReader<byte> reader, out int value)
        {
            if (!BitConverter.IsLittleEndian)
            {
                return reader.TryRead(out value);
            }

            return TryReadReverseEndianness(ref reader, out value);
        }

        private static bool TryReadReverseEndianness(ref BufferReader<byte> reader, out int value)
        {
            if (reader.TryRead(out value))
            {
                value = BinaryPrimitives.ReverseEndianness(value);
                return true;
            }

            return false;
        }

        public static bool TryReadLittleEndian(ref this BufferReader<byte> reader, out uint value)
        {
            if (TryReadLittleEndian(ref reader, out int signedvalue))
            {
                value = unchecked((uint)signedvalue);
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryReadBigEndian(ref this BufferReader<byte> reader, out uint value)
        {
            if (TryReadBigEndian(ref reader, out int signedvalue))
            {
                value = unchecked((uint)signedvalue);
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryReadLittleEndian(ref this BufferReader<byte> reader, out long value)
        {
            if (BitConverter.IsLittleEndian)
            {
                return reader.TryRead(out value);
            }

            return TryReadReverseEndianness(ref reader, out value);
        }

        public static bool TryReadBigEndian(ref this BufferReader<byte> reader, out long value)
        {
            if (!BitConverter.IsLittleEndian)
            {
                return reader.TryRead(out value);
            }

            return TryReadReverseEndianness(ref reader, out value);
        }

        private static bool TryReadReverseEndianness(ref BufferReader<byte> reader, out long value)
        {
            if (reader.TryRead(out value))
            {
                value = BinaryPrimitives.ReverseEndianness(value);
                return true;
            }

            return false;
        }


        public static bool TryReadLittleEndian(ref this BufferReader<byte> reader, out ulong value)
        {
            if (TryReadLittleEndian(ref reader, out long signedvalue))
            {
                value = unchecked((ulong)signedvalue);
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryReadBigEndian(ref this BufferReader<byte> reader, out ulong value)
        {
            if (TryReadBigEndian(ref reader, out long signedvalue))
            {
                value = unchecked((ulong)signedvalue);
                return true;
            }

            value = default;
            return false;
        }
    }
}
