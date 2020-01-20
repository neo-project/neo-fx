using System;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace NeoFx.Storage
{
    public static class VarSizeHelpers
    {
        public static int GetVarSize(ulong value)
        {
            if (value < 0xfd)
            {
                return sizeof(byte);
            }

            if (value < 0xffff)
            {
                return sizeof(byte) + sizeof(ushort);
            }

            if (value < 0xffffffff)
            {
                return sizeof(byte) + sizeof(uint);
            }

            return sizeof(byte) + sizeof(ulong);
        }

        public static int GetVarSize(this string value)
            => GetVarSize(value.AsSpan());

        public static int GetVarSize(this ReadOnlySpan<char> value)
        {
            int size = System.Text.Encoding.UTF8.GetByteCount(value);
            return GetVarSize((ulong)size) + size;
        }

        public static int GetVarSize(this ReadOnlyMemory<byte> value)
            => value.Span.GetVarSize();

        public static int GetVarSize(this ReadOnlySpan<byte> value)
            => GetVarSize((ulong)value.Length) + value.Length;

        public static int GetVarSize<T>(this ReadOnlyMemory<T> memory, Func<T, int> getSize)
            => memory.Span.GetVarSize(getSize);

        public static int GetVarSize<T>(this ReadOnlySpan<T> span, Func<T, int> getSize)
        {
            int size = 0;
            for (int i = 0; i < span.Length; i++)
            {
                size += getSize(span[i]);
            }
            return GetVarSize((ulong)span.Length) + size;
        }

        public static int GetVarSize<T>(this ReadOnlyMemory<T> memory, int size)
            => memory.Span.GetVarSize(size);

        public static int GetVarSize<T>(this ReadOnlySpan<T> span, int size)
            => GetVarSize((ulong)span.Length) + (span.Length * size);

        public static bool TryReadVarInt(ref this SpanReader<byte> reader, out ulong value)
        {
            return TryReadVarInt(ref reader, ulong.MaxValue, out value);
        }

        public static bool TryReadVarInt(ref this SpanReader<byte> reader, ulong max, out ulong value)
        {
            static bool CheckMax(ulong value, ulong max, out ulong outValue)
            {
                if (value <= max)
                {
                    outValue = value;
                    return true;
                }

                outValue = default;
                return false;
            }

            if (reader.TryRead(out byte b))
            {
                if (b < 0xfd)
                {
                    return CheckMax(b, max, out value);
                }

                if (b == 0xfd
                    && reader.TryRead(out ushort @ushort))
                {
                    return CheckMax(@ushort, max, out value);
                }

                if (b == 0xfe
                    && reader.TryRead(out uint @uint))
                {
                    return CheckMax(@uint, max, out value);
                }

                if (b == 0xff
                    && reader.TryRead(out ulong @ulong))
                {
                    return CheckMax(@ulong, max, out value);
                }
            }

            value = default;
            return false;
        }

        public static bool TryReadVarString(ref this SpanReader<byte> reader, out string value)
        {
            return TryReadVarString(ref reader, 0x1000000, out value);
        }

        public static bool TryReadVarString(ref this SpanReader<byte> reader, uint max, out string value)
        {
            if (reader.TryReadVarInt(max, out var length)
                && length < int.MaxValue)
            {
                Span<byte> buffer = stackalloc byte[(int)length];
                reader.TryCopyTo(buffer);

                value = System.Text.Encoding.UTF8.GetString(buffer);
                return true;
            }

            value = string.Empty;
            return false;
        }

        public static bool TryReadByteArray(ref this SpanReader<byte> reader, int length, out ImmutableArray<byte> value)
        {
            // check length first to avoid allocating array if reader doesn't have enough data
            if (reader.Length >= length)
            {
                var _value = new byte[length];
                if (reader.TryCopyTo(_value.AsSpan()))
                {
                    reader.Advance(length);
                    value = Unsafe.As<byte[], ImmutableArray<byte>>(ref _value);
                    return true;
                }
            }

            value = default;
            return false;
        }

        public static bool TryReadVarArray(ref this SpanReader<byte> reader, out ImmutableArray<byte> value)
        {
            return TryReadVarArray(ref reader, 0x1000000, out value);
        }

        public static bool TryReadVarArray(ref this SpanReader<byte> reader, uint max, out ImmutableArray<byte> value)
        {
            if (reader.TryReadVarInt(max, out var length)
                && length <= int.MaxValue
                && reader.TryReadByteArray((int)length, out var _value))
            {
                value = _value;
                return true;
            }

            value = default;
            return false;
        }

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
