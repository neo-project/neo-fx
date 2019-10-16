using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;

namespace NeoFx.Storage
{
    public static class IBufferWriterHelpers
    {
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

        public delegate void WriteItem<T>(IBufferWriter<byte> writer, in T item);

        public static void WriteVarArray<T>(this IBufferWriter<byte> writer, ReadOnlySpan<T> span, WriteItem<T> writeItem)
        {
            writer.WriteVarInt(span.Length);
            for (int i = 0; i < span.Length; i++)
            {
                writeItem(writer, span[i]);
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
