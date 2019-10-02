using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;

namespace NeoFx.Models
{
    public static class SequenceReaderExtensions
    {
        public delegate bool TryConvert<T>(ReadOnlySpan<byte> span, out T value);

        public static bool TryRead<T>(ref this SequenceReader<byte> reader, int size, TryConvert<T> tryConvert, out T value) where T : struct
        {
            var span = reader.UnreadSpan;
            if (span.Length >= size)
            {
                if (tryConvert(span, out value))
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

            value = default;
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

                if (b == 0xfd && reader.TryReadUInt16LittleEndian(out ushort @ushort))
                {
                    value = @ushort;
                    return true;
                }

                if (b == 0xfe && reader.TryReadUInt32LittleEndian(out uint @uint))
                {
                    value = @uint;
                    return true;
                }

                if (b == 0xff && reader.TryReadUInt64LittleEndian(out ulong @ulong))
                {
                    value = @ulong;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public static bool TryReadVarArray(ref this SequenceReader<byte> reader, out ReadOnlyMemory<byte> value)
        {
            if (reader.TryReadVarInt(out var count))
            {
                Debug.Assert(count < int.MaxValue);

                var buffer = new byte[count];
                if (reader.TryCopyTo(buffer))
                {
                    value = buffer;
                    reader.Advance((long)count);
                    return true;
                }
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

        public static bool TryReadInt16LittleEndian(ref this SequenceReader<byte> reader, out short value) =>
            reader.TryRead(sizeof(short), BinaryPrimitives.TryReadInt16LittleEndian, out value);

        public static bool TryReadInt32LittleEndian(ref this SequenceReader<byte> reader, out int value) =>
            reader.TryRead(sizeof(int), BinaryPrimitives.TryReadInt32LittleEndian, out value);

        public static bool TryReadInt64LittleEndian(ref this SequenceReader<byte> reader, out long value) =>
            reader.TryRead(sizeof(long), BinaryPrimitives.TryReadInt64LittleEndian, out value);

        public static bool TryReadUInt16LittleEndian(ref this SequenceReader<byte> reader, out ushort value) =>
            reader.TryRead(sizeof(ushort), BinaryPrimitives.TryReadUInt16LittleEndian, out value);

        public static bool TryReadUInt32LittleEndian(ref this SequenceReader<byte> reader, out uint value) =>
            reader.TryRead(sizeof(uint), BinaryPrimitives.TryReadUInt32LittleEndian, out value);

        public static bool TryReadUInt64LittleEndian(ref this SequenceReader<byte> reader, out ulong value) =>
            reader.TryRead(sizeof(ulong), BinaryPrimitives.TryReadUInt64LittleEndian, out value);
    }
}
