using NeoFx.Models;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace NeoFx.Storage
{
    public ref struct SpanWriter<T>
    {
        private readonly Span<T> span;
        private int position;

        public SpanWriter(Span<T> span)
        {
            this.span = span;
            position = 0;
        }

        public Span<T> Span => span.Slice(position);
        public ReadOnlySpan<T> Contents => span.Slice(0, position);

        public int Length => span.Length - position;

        public void Advance(int size)
        {
            position += size;
        }
    }

    public static class BinaryWriter
    {
        public static bool TryWrite(ref this SpanWriter<byte> writer, byte value)
        {
            if (writer.Length > 0)
            {
                writer.Span[0] = value;
                writer.Advance(1);
                return true;
            }

            return false;
        }

        public static bool TryWrite(ref this SpanWriter<byte> writer, ushort value)
        {
            if (BinaryPrimitives.TryWriteUInt16LittleEndian(writer.Span, value))
            {
                writer.Advance(sizeof(ushort));
                return true;
            }
            return false;
        }

        public static bool TryWrite(ref this SpanWriter<byte> writer, uint value)
        {
            if (BinaryPrimitives.TryWriteUInt32LittleEndian(writer.Span, value))
            {
                writer.Advance(sizeof(uint));
                return true;
            }
            return false;
        }

        public static bool TryWrite(ref this SpanWriter<byte> writer, ulong value)
        {
            if (BinaryPrimitives.TryWriteUInt64LittleEndian(writer.Span, value))
            {
                writer.Advance(sizeof(ulong));
                return true;
            }
            return false;
        }

        public static bool TryWrite(ref this SpanWriter<byte> writer, ReadOnlySpan<byte> value)
        {
            if (value.TryCopyTo(writer.Span))
            {
                writer.Advance(value.Length);
                return true;
            }
            return false;
        }

        public static bool TryWriteVarInt(ref this SpanWriter<byte> writer, int value)
        {
            Debug.Assert(value >= 0);
            return TryWriteVarInt(ref writer, (ulong)value);
        }

        public static bool TryWriteVarInt(ref this SpanWriter<byte> writer, ulong value)
        {
            if (value < 0xfd)
            {
                return writer.TryWrite((byte)value);
            }

            if (value < 0xffff)
            {
                return writer.TryWrite(0xfd)
                    && writer.TryWrite((ushort)value);
            }

            if (value < 0xffffffff)
            {
                return writer.TryWrite(0xfe)
                    && writer.TryWrite((uint)value);
            }

            return writer.TryWrite(0xff)
                && writer.TryWrite(value);
        }

        public static bool TryWriteVarArray(ref this SpanWriter<byte> writer, ReadOnlyMemory<byte> memory)
        {
            return TryWriteVarArray(ref writer, memory.Span);
        }

        public static bool TryWriteVarArray(ref this SpanWriter<byte> writer, ReadOnlySpan<byte> span)
        {
            return writer.TryWriteVarInt(span.Length)
                && writer.TryWrite(span);
        }

        public delegate bool TryWriteItem<T>(ref SpanWriter<byte> writer, in T item);

        public static bool TryWriteVarArray<T>(ref this SpanWriter<byte> writer, ReadOnlyMemory<T> memory, TryWriteItem<T> tryWriteItem)
        {
            return TryWriteVarArray(ref writer, memory.Span, tryWriteItem);
        }
            
        public static bool TryWriteVarArray<T>(ref this SpanWriter<byte> writer, ReadOnlySpan<T> span, TryWriteItem<T> tryWriteItem)
        {
            if (writer.TryWriteVarInt(span.Length))
            {
                for (int i = 0; i < span.Length; i++)
                {
                    if (!tryWriteItem(ref writer, span[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        public static bool TryWrite(ref SpanWriter<byte> writer, in TransactionAttribute value)
        {
            return false;
        }

        public static bool TryWrite(ref SpanWriter<byte> writer, in CoinReference value)
        {
            return false;
        }

        public static bool TryWrite(ref SpanWriter<byte> writer, in TransactionOutput value)
        {
            return false;
        }
    }
}
