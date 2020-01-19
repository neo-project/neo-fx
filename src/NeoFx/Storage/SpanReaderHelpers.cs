using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NeoFx.Storage
{
    public static class SpanReaderExtensions
    {
        public static unsafe bool TryRead<T>(ref this SpanReader<byte> reader, out T value)
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

        private static unsafe bool TryReadMultisegment<T>(ref SpanReader<byte> reader, out T value)
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

        public static bool TryRead(ref this SpanReader<byte> reader, out sbyte value)
        {
            if (TryRead(ref reader, out byte byteValue))
            {
                value = unchecked((sbyte)byteValue);
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryReadBigEndian(ref this SpanReader<byte> reader, out short value)
        {
            if (!BitConverter.IsLittleEndian)
            {
                return reader.TryRead(out value);
            }

            return TryReadReverseEndianness(ref reader, out value);
        }

        public static bool TryReadBigEndian(ref this SpanReader<byte> reader, out ushort value)
        {
            if (TryReadBigEndian(ref reader, out short shortValue))
            {
                value = unchecked((ushort)shortValue);
                return true;
            }

            value = default;
            return false;
        }

        private static bool TryReadReverseEndianness(ref SpanReader<byte> reader, out short value)
        {
            if (reader.TryRead(out value))
            {
                value = BinaryPrimitives.ReverseEndianness(value);
                return true;
            }

            return false;
        }

        public static bool TryReadBigEndian(ref this SpanReader<byte> reader, out int value)
        {
            if (!BitConverter.IsLittleEndian)
            {
                return reader.TryRead(out value);
            }

            return TryReadReverseEndianness(ref reader, out value);
        }

        public static bool TryReadBigEndian(ref this SpanReader<byte> reader, out uint value)
        {
            if (TryReadBigEndian(ref reader, out int intValue))
            {
                value = unchecked((uint)intValue);
                return true;
            }

            value = default;
            return false;
        }

        private static bool TryReadReverseEndianness(ref SpanReader<byte> reader, out int value)
        {
            if (reader.TryRead(out value))
            {
                value = BinaryPrimitives.ReverseEndianness(value);
                return true;
            }

            return false;
        }

        public static bool TryReadBigEndian(ref this SpanReader<byte> reader, out long value)
        {
            if (!BitConverter.IsLittleEndian)
            {
                return reader.TryRead(out value);
            }

            return TryReadReverseEndianness(ref reader, out value);
        }

        public static bool TryReadBigEndian(ref this SpanReader<byte> reader, out ulong value)
        {
            if (TryReadBigEndian(ref reader, out long longValue))
            {
                value = unchecked((ulong)longValue);
                return true;
            }

            value = default;
            return false;
        }

        private static bool TryReadReverseEndianness(ref SpanReader<byte> reader, out long value)
        {
            if (reader.TryRead(out value))
            {
                value = BinaryPrimitives.ReverseEndianness(value);
                return true;
            }

            return false;
        }

        public static unsafe bool TryReadBigEndian(ref this SpanReader<byte> reader, out float value)
        {
            if (TryReadBigEndian(ref reader, out int intValue))
            {
                value = *(float*)&intValue;
                return true;
            }

            value = default;
            return false;
        }

        public static unsafe bool TryReadBigEndian(ref this SpanReader<byte> reader, out double value)
        {
            if (TryReadBigEndian(ref reader, out long longValue))
            {
                value = *(double*)&longValue;
                return true;
            }

            value = default;
            return false;
        }

        //public static bool TryReadByteArray(ref this SpanReader<byte> reader, int length, [NotNullWhen(true)] out byte[]? value)
        //{
        //    // check length first to avoid allocating array if reader doesn't have enough data
        //    if (reader.Length >= length)
        //    {
        //        var _value = new byte[length];
        //        if (reader.TryCopyTo(_value.AsSpan()))
        //        {
        //            reader.Advance(length);
        //            value = _value;
        //            return true;
        //        }
        //    }
        //    value = null;
        //    return false;
        //}

        //public static bool TryReadVarInt(ref this SpanReader<byte> reader, out ulong value)
        //{
        //    return TryReadVarInt(ref reader, ulong.MaxValue, out value);
        //}

        //public static bool TryReadVarInt(ref this SpanReader<byte> reader, ulong max, out ulong value)
        //{
        //    static bool CheckMax(ulong value, ulong max, out ulong outValue)
        //    {
        //        if (value <= max)
        //        {
        //            outValue = value;
        //            return true;
        //        }

        //        outValue = default;
        //        return false;
        //    }

        //    if (reader.TryRead(out byte b))
        //    {
        //        if (b < 0xfd)
        //        {
        //            return CheckMax(b, max, out value);
        //        }

        //        if (b == 0xfd
        //            && reader.TryRead(out ushort @ushort))
        //        {
        //            return CheckMax(@ushort, max, out value);
        //        }

        //        if (b == 0xfe
        //            && reader.TryRead(out uint @uint))
        //        {
        //            return CheckMax(@uint, max, out value);
        //        }

        //        if (b == 0xff
        //            && reader.TryRead(out ulong @ulong))
        //        {
        //            return CheckMax(@ulong, max, out value);
        //        }
        //    }

        //    value = default;
        //    return false;
        //}

        //public static bool TryReadVarString(ref this SpanReader<byte> reader, out string value)
        //{
        //    return TryReadVarString(ref reader, 0x1000000, out value);
        //}

        //public static bool TryReadVarString(ref this SpanReader<byte> reader, uint max, out string value)
        //{
        //    if (reader.TryReadVarInt(max, out var length)
        //        && length < int.MaxValue)
        //    {
        //        Span<byte> buffer = stackalloc byte[(int)length];
        //        if (reader.TryCopyTo(buffer))
        //        {
        //            value = System.Text.Encoding.UTF8.GetString(buffer);
        //            return true;
        //        }
        //    }

        //    value = string.Empty;
        //    return false;
        //}

        //public static bool TryReadVarArray(ref this SpanReader<byte> reader, [NotNullWhen(true)] out byte[]? value)
        //{
        //    return TryReadVarArray(ref reader, 0x1000000, out value);
        //}

        //public static bool TryReadVarArray(ref this SpanReader<byte> reader, uint max, [NotNullWhen(true)] out byte[]? value)
        //{
        //    if (reader.TryReadVarInt(max, out var length)
        //        && length <= int.MaxValue
        //        && reader.TryReadByteArray((int)length, out var _value))
        //    {
        //        value = _value;
        //        return true;
        //    }

        //    value = null;
        //    return false;
        //}

        //public delegate bool TryReadItem<T>(ref SpanReader<byte> reader, out T value);

        //public static bool TryReadVarArray<T>(ref this SpanReader<byte> reader, TryReadItem<T> tryReadItem, [NotNullWhen(true)] out T[]? value)
        //{
        //    return TryReadVarArray<T>(ref reader, 0x1000000, tryReadItem, out value);
        //}

        //public static bool TryReadVarArray<T>(ref this SpanReader<byte> reader, uint max, TryReadItem<T> tryReadItem, [NotNullWhen(true)] out T[]? value)
        //{
        //    if (reader.TryReadVarInt(max, out var length))
        //    {
        //        Debug.Assert(length <= int.MaxValue);

        //        var buffer = new T[length];
        //        for (int index = 0; index < (int)length; index++)
        //        {
        //            if (!tryReadItem(ref reader, out buffer[index]))
        //            {
        //                value = null;
        //                return false;
        //            }
        //        }

        //        value = buffer;
        //        return true;
        //    }

        //    value = null;
        //    return false;
        //}
    }

}
