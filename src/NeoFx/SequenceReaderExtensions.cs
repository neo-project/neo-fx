using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace NeoFx
{
    public static class SequenceReaderExtensions
    {
        public delegate bool TryConvert<T>(ReadOnlySpan<byte> span, out T value);

        public static bool TryRead<T>(ref this SequenceReader<byte> reader, int size, TryConvert<T> tryConvert, [MaybeNull] out T value)
        {
            var span = reader.UnreadSpan;
            if (span.Length >= size)
            {
                if (tryConvert(span.Slice(0, size), out value))
                {
                    reader.Advance(size);
                    return true;
                }
                else
                {
                    return false;
                }
            }

            Span<byte> buffer = stackalloc byte[size];
            if (reader.TryCopyTo(buffer))
            {
                if (tryConvert(buffer, out value))
                {
                    reader.Advance(size);
                    return true;
                }
                else
                {
                    return false;
                }
            }

#pragma warning disable CS8653 // A default expression introduces a null value for a type parameter.
            value = default;
#pragma warning restore CS8653 // A default expression introduces a null value for a type parameter.
            return false;
        }

        public static bool TryReadVarInt(ref this SequenceReader<byte> reader, out ulong value)
        {
            if (reader.TryRead(out byte b))
            {
                if (b < 0xfd)
                {
                    value = b;
                    return true;
                }

                if (b == 0xfd 
                    && reader.TryRead(sizeof(ushort), BinaryPrimitives.TryReadUInt16LittleEndian, out ushort @ushort))
                {
                    value = @ushort;
                    return true;
                }

                if (b == 0xfe 
                    && reader.TryRead(sizeof(uint), BinaryPrimitives.TryReadUInt32LittleEndian, out uint @uint))
                {
                    value = @uint;
                    return true;
                }

                if (b == 0xfe 
                    && reader.TryRead(sizeof(ulong), BinaryPrimitives.TryReadUInt64LittleEndian, out ulong @ulong))
                {
                    value = @ulong;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public static bool TryReadByteArray(ref this SequenceReader<byte> reader, int count, out ReadOnlyMemory<byte> value)
        {
            var buffer = new byte[count];
            if (reader.TryCopyTo(buffer))
            {
                value = buffer;
                reader.Advance((long)count);
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryReadVarArray(ref this SequenceReader<byte> reader, out ReadOnlyMemory<byte> value)
        {
            if (reader.TryReadVarInt(out var count))
            {
                Debug.Assert(count < int.MaxValue);
                return reader.TryReadByteArray((int)count, out value);
            }

            value = default;
            return false;
        }

        public delegate bool TryReadItem<T>(ref SequenceReader<byte> reader, out T value);

        public static bool TryReadVarArray<T>(ref this SequenceReader<byte> reader, TryReadItem<T> tryReadItem, out ReadOnlyMemory<T> memory)
        {
            if (reader.TryReadVarInt(out var count))
            {
                Debug.Assert(count <= int.MaxValue);

                var buffer = new T[count];
                for (int index = 0; index < (int)count; index++)
                {
                    if (!tryReadItem(ref reader, out buffer[index]))
                    {
                        memory = default;
                        return false;
                    }
                }

                memory = buffer;
                return true;
            }

            memory = default;
            return false;
        }

        public static bool TryReadVarString(ref this SequenceReader<byte> reader, out string value)
        {
            static bool TryConvertString(ReadOnlySpan<byte> span, out string value)
            {
                value = System.Text.Encoding.UTF8.GetString(span);
                return true;
            }

            if (reader.TryReadVarInt(out var length)
                && length < int.MaxValue
                && reader.TryRead<string>((int)length, TryConvertString, out var _value))
            {
                value = _value ?? string.Empty;
                return true;
            }

            value = string.Empty;
            return false;
        }
    }
}
