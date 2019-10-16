using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace NeoFx.Storage
{
    public static class SpanReaderHelpers
    {
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
                    && reader.TryRead(sizeof(ushort), BinaryPrimitives.TryReadUInt16LittleEndian, out ushort @ushort))
                {
                    return CheckMax(@ushort, max, out value);
                }

                if (b == 0xfe
                    && reader.TryRead(sizeof(uint), BinaryPrimitives.TryReadUInt32LittleEndian, out uint @uint))
                {
                    return CheckMax(@uint, max, out value);
                }

                if (b == 0xfe
                    && reader.TryRead(sizeof(ulong), BinaryPrimitives.TryReadUInt64LittleEndian, out ulong @ulong))
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
            static bool TryConvertString(ReadOnlySpan<byte> span, out string value)
            {
                value = System.Text.Encoding.UTF8.GetString(span);
                return true;
            }

            if (reader.TryReadVarInt(max, out var length)
                && length < int.MaxValue
                && reader.TryRead<string>((int)length, TryConvertString, out var _value))
            {
                value = _value ?? string.Empty;
                return true;
            }

            value = string.Empty;
            return false;
        }

        public static bool TryReadByteArray(ref this SpanReader<byte> reader, int length, [NotNullWhen(true)] out byte[]? value)
        {
            // check length first to avoid allocating array if reader doesn't have enough data
            if (reader.Length >= length)
            {
                var _value = new byte[length];
                if (reader.TryCopyTo(_value.AsSpan())
                    && reader.TryAdvance(length))
                {
                    value = _value;
                    return true;
                }
            }
            value = null;
            return false;
        }

        public static bool TryReadVarArray(ref this SpanReader<byte> reader, [NotNullWhen(true)] out byte[]? value)
        {
            return TryReadVarArray(ref reader, 0x1000000, out value);
        }

        public static bool TryReadVarArray(ref this SpanReader<byte> reader, uint max, [NotNullWhen(true)] out byte[]? value)
        {
            if (reader.TryReadVarInt(max, out var length)
                && length <= int.MaxValue
                && reader.TryReadByteArray((int)length, out var _value))
            {
                value = _value;
                return true;
            }

            value = null;
            return false;
        }

        public delegate bool TryReadItem<T>(ref SpanReader<byte> reader, out T value);

        public static bool TryReadVarArray<T>(ref this SpanReader<byte> reader, TryReadItem<T> tryReadItem, [NotNullWhen(true)] out T[]? value)
        {
            return TryReadVarArray<T>(ref reader, 0x1000000, tryReadItem, out value);
        }

        public static bool TryReadVarArray<T>(ref this SpanReader<byte> reader, uint max, TryReadItem<T> tryReadItem, [NotNullWhen(true)] out T[]? value)
        {
            if (reader.TryReadVarInt(max, out var length))
            {
                Debug.Assert(length <= int.MaxValue);

                var buffer = new T[length];
                for (int index = 0; index < (int)length; index++)
                {
                    if (!tryReadItem(ref reader, out buffer[index]))
                    {
                        value = null;
                        return false;
                    }
                }

                value = buffer;
                return true;
            }

            value = null;
            return false;
        }
    }
}
