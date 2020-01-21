using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Diagnostics;
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

        public static bool TryReadVarInt(ref this BufferReader<byte> reader, out ulong value)
        {
            return TryReadVarInt(ref reader, ulong.MaxValue, out value);
        }

        public static bool TryReadVarInt(ref this BufferReader<byte> reader, ulong max, out ulong value)
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
                    && reader.TryReadLittleEndian(out ushort @ushort))
                {
                    return CheckMax(@ushort, max, out value);
                }

                if (b == 0xfe
                    && reader.TryReadLittleEndian(out uint @uint))
                {
                    return CheckMax(@uint, max, out value);
                }

                if (b == 0xff
                    && reader.TryReadLittleEndian(out ulong @ulong))
                {
                    return CheckMax(@ulong, max, out value);
                }
            }

            value = default;
            return false;
        }

        public static bool TryReadVarString(ref this BufferReader<byte> reader, out string value)
        {
            return TryReadVarString(ref reader, 0x1000000, out value);
        }

        public static bool TryReadVarString(ref this BufferReader<byte> reader, uint max, out string value)
        {
            if (reader.TryReadVarInt(max, out var length)
                && length < int.MaxValue)
            {
                Span<byte> buffer = stackalloc byte[(int)length];
                reader.TryCopyTo(buffer);
                reader.Advance((int)length);

                value = System.Text.Encoding.UTF8.GetString(buffer);
                return true;
            }

            value = string.Empty;
            return false;
        }

        public static bool TryReadByteArray(ref this BufferReader<byte> reader, int length, out ImmutableArray<byte> value)
        {
            // check length first to avoid allocating array if reader doesn't have enough data
            if (reader.Length >= length)
            {
                var array = new byte[length];
                if (reader.TryCopyTo(array.AsSpan()))
                {
                    reader.Advance(length);
                    value = Unsafe.As<byte[], ImmutableArray<byte>>(ref array);
                    return true;
                }
            }

            value = default;
            return false;
        }

        public static bool TryReadVarArray(ref this BufferReader<byte> reader, out ImmutableArray<byte> value)
        {
            return TryReadVarArray(ref reader, 0x1000000, out value);
        }

        public static bool TryReadVarArray(ref this BufferReader<byte> reader, uint max, out ImmutableArray<byte> value)
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

        public delegate bool TryReadItem<T>(ref BufferReader<byte> reader, out T value);

        public static bool TryReadVarArray<T>(ref this BufferReader<byte> reader, TryReadItem<T> tryReadItem, out ImmutableArray<T> value)
        {
            return TryReadVarArray<T>(ref reader, 0x1000000, tryReadItem, out value);
        }

        public static bool TryReadVarArray<T>(ref this BufferReader<byte> reader, uint max, TryReadItem<T> tryReadItem, out ImmutableArray<T> value)
        {
            if (reader.TryReadVarInt(max, out var length))
            {
                Debug.Assert(length <= int.MaxValue);

                var array = new T[(int)length];
                for (int index = 0; index < (int)length; index++)
                {
                    if (!tryReadItem(ref reader, out array[index]))
                    {
                        value = default;
                        return false;
                    }
                }

                value = Unsafe.As<T[], ImmutableArray<T>>(ref array);
                return true;
            }

            value = default;
            return false;
        }

        public static void WriteVarInt(this IBufferWriter<byte> writer, int value)
        {
            Debug.Assert(value >= 0);
            writer.WriteVarInt((ulong)value);
        }

        public static void WriteVarInt(this IBufferWriter<byte> writer, ulong value)
        {
            if (value < 0xfd)
            {
                writer.GetSpan(1)[0] = (byte)value;
                writer.Advance(1);
                return;
            }

            if (value < 0xffff)
            {
                var span = writer.GetSpan(3);
                span[0] = 0xfd;
                BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(1, 2), (ushort)value);
                writer.Advance(3);
                return;
            }

            if (value < 0xffffffff)
            {
                var span = writer.GetSpan(5);
                span[0] = 0xfe;
                BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(1, 4), (uint)value);
                writer.Advance(5);
                return;
            }

            {
                var span = writer.GetSpan(9);
                span[0] = 0xff;
                BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(1, 8), value);
                writer.Advance(9);
            }
        }

        public static void WriteVarArray(this IBufferWriter<byte> writer, ReadOnlySpan<byte> span)
        {
            writer.WriteVarInt(span.Length);
            writer.Write(span);
        }

        public static void WriteVarArray<T>(this IBufferWriter<byte> writer, ReadOnlySpan<T> span)
            where T : IWritable<T>
        {
            writer.WriteVarInt(span.Length);
            for (int i = 0; i < span.Length; i++)
            {
                span[i].Write(writer);
            }
        }

        public static void WriteVarString(this IBufferWriter<byte> writer, ReadOnlySpan<char> @string)
        {
            var length = System.Text.Encoding.UTF8.GetByteCount(@string);
            writer.WriteVarInt(length);

            var span = writer.GetSpan(length);
            System.Text.Encoding.UTF8.GetBytes(@string, span);
            writer.Advance(length);
        }
    }
}
