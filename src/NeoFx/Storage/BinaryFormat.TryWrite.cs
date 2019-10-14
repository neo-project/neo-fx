using NeoFx.Models;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace NeoFx.Storage
{
    public static partial class BinaryFormat
    {
        //public static bool TryWriteBytes(this CoinReference input, Span<byte> span)
        //{
        //    return span.Length >= CoinReferenceSize
        //        && input.PrevHash.TryWrite(span)
        //        && BinaryPrimitives.TryWriteUInt16LittleEndian(span.Slice(UInt256.Size), input.PrevIndex);
        //}

        //public static bool TryWriteBytes(this TransactionOutput output, Span<byte> span)
        //{
        //    return span.Length >= TransactionOutputSize
        //        && output.AssetId.TryWrite(span)
        //        && output.Value.TryWrite(span.Slice(UInt256.Size))
        //        && output.ScriptHash.TryWrite(span.Slice(UInt256.Size + Fixed8.Size));
        //}

        public static bool TryWriteBytes(this StorageKey key, Span<byte> span)
        {
            if (span.Length >= key.GetSize() && key.ScriptHash.TryWrite(span))
            {
                span = span.Slice(UInt160.Size);
                var keySpan = key.Key.Span;

                while (keySpan.Length >= StorageKeyBlockSize)
                {
                    keySpan.Slice(0, StorageKeyBlockSize).CopyTo(span);
                    span[StorageKeyBlockSize] = 0;

                    keySpan = keySpan.Slice(StorageKeyBlockSize);
                    span = span.Slice(StorageKeyBlockSize + 1);
                }

                Debug.Assert(span.Length == StorageKeyBlockSize + 1);

                keySpan.CopyTo(span);
                span.Slice(keySpan.Length).Clear();
                span[StorageKeyBlockSize] = (byte)(StorageKeyBlockSize - keySpan.Length);

                return true;
            }

            return false;
        }

        //public static bool TryWrite(ref this SpanWriter<byte> writer, byte value)
        //{
        //    if (writer.Length > 0)
        //    {
        //        writer.Span[0] = value;
        //        writer.Advance(1);
        //        return true;
        //    }

        //    return false;
        //}

        //public static bool TryWrite(ref this SpanWriter<byte> writer, ushort value)
        //{
        //    if (BinaryPrimitives.TryWriteUInt16LittleEndian(writer.Span, value))
        //    {
        //        writer.Advance(sizeof(ushort));
        //        return true;
        //    }
        //    return false;
        //}

        //public static bool TryWrite(ref this SpanWriter<byte> writer, uint value)
        //{
        //    if (BinaryPrimitives.TryWriteUInt32LittleEndian(writer.Span, value))
        //    {
        //        writer.Advance(sizeof(uint));
        //        return true;
        //    }
        //    return false;
        //}

        //public static bool TryWrite(ref this SpanWriter<byte> writer, ulong value)
        //{
        //    if (BinaryPrimitives.TryWriteUInt64LittleEndian(writer.Span, value))
        //    {
        //        writer.Advance(sizeof(ulong));
        //        return true;
        //    }
        //    return false;
        //}

        //public static bool TryWrite(ref this SpanWriter<byte> writer, ReadOnlySpan<byte> value)
        //{
        //    if (value.TryCopyTo(writer.Span))
        //    {
        //        writer.Advance(value.Length);
        //        return true;
        //    }
        //    return false;
        //}

        //public static bool TryWriteVarInt(this Span<byte> span, int value, out int bytesWritten)
        //{
        //    Debug.Assert(value >= 0);
        //    return span.TryWriteVarInt((ulong)value, out bytesWritten);
        //}

        //public static bool TryWriteVarInt(this Span<byte> span, ulong value, out int bytesWritten)
        //{
        //    if (value < 0xfd && span.Length >= 1)
        //    {
        //        span[0] = (byte)value;
        //        bytesWritten = 1;
        //        return true;
        //    }

        //    if (value < 0xffff
        //        && span.Length >= 3
        //        && BinaryPrimitives.TryWriteUInt16LittleEndian(span.Slice(1, 2), (ushort)value))
        //    {
        //        span[0] = 0xfd;
        //        bytesWritten = 3;
        //        return true;
        //    }

        //    if (value < 0xffffffff
        //        && span.Length >= 5
        //        && BinaryPrimitives.TryWriteUInt32LittleEndian(span.Slice(1, 4), (uint)value))
        //    {
        //        span[0] = 0xfe;
        //        bytesWritten = 5;
        //        return true;
        //    }

        //    if (span.Length >= 9
        //        && BinaryPrimitives.TryWriteUInt64LittleEndian(span.Slice(1, 4), value))
        //    {
        //        span[0] = 0xfe;
        //        bytesWritten = 9;
        //        return true;
        //    }

        //    bytesWritten = default;
        //    return false;
        //}

        //public static bool TryWriteVarInt(ref this SpanWriter<byte> writer, int value)
        //{
        //    Debug.Assert(value >= 0);
        //    return TryWriteVarInt(ref writer, (ulong)value);
        //}

        //public static bool TryWriteVarInt(ref this SpanWriter<byte> writer, ulong value)
        //{
        //    if (writer.Span.TryWriteVarInt(value, out var bytesWritten))
        //    {
        //        writer.Advance(bytesWritten);
        //        return true;
        //    }
        //    return false;
        //}

        //public static bool TryWriteVarArray(ref this SpanWriter<byte> writer, ReadOnlyMemory<byte> memory)
        //{
        //    return TryWriteVarArray(ref writer, memory.Span);
        //}

        //public static bool TryWriteVarArray(ref this SpanWriter<byte> writer, ReadOnlySpan<byte> span)
        //{
        //    return writer.TryWriteVarInt(span.Length)
        //        && writer.TryWrite(span);
        //}

        //public delegate bool TryWriteItem<T>(ref SpanWriter<byte> writer, in T item);

        //public static bool TryWriteVarArray<T>(ref this SpanWriter<byte> writer, ReadOnlyMemory<T> memory, TryWriteItem<T> tryWriteItem)
        //{
        //    return TryWriteVarArray(ref writer, memory.Span, tryWriteItem);
        //}

        //public static bool TryWriteVarArray<T>(ref this SpanWriter<byte> writer, ReadOnlySpan<T> span, TryWriteItem<T> tryWriteItem)
        //{
        //    if (writer.TryWriteVarInt(span.Length))
        //    {
        //        for (int i = 0; i < span.Length; i++)
        //        {
        //            if (!tryWriteItem(ref writer, span[i]))
        //            {
        //                return false;
        //            }
        //        }

        //        return true;
        //    }

        //    return false;
        //}

        //public static bool TryWrite(ref SpanWriter<byte> writer, in TransactionAttribute value)
        //{

        //    return false;
        //}

        //public static bool TryWrite(ref SpanWriter<byte> writer, in CoinReference value)
        //{
        //    if (value.TryWriteBytes(writer.Span))
        //    {
        //        writer.Advance(CoinReferenceSize);
        //        return true;
        //    }

        //    return false;
        //}

        //public static bool TryWrite(ref SpanWriter<byte> writer, in TransactionOutput value)
        //{
        //    if (value.TryWriteBytes(writer.Span))
        //    {
        //        writer.Advance(TransactionOutputSize);
        //        return true;
        //    }

        //    return false;
        //}
    }
}
