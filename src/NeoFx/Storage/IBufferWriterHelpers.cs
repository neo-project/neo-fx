using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NeoFx.Storage
{
    public interface IWritable<T>
    {
        void Write(IBufferWriter<byte> writer);
    }

    public static class IBufferWriterHelpers
    {
        public static unsafe void WriteLittleEndian<T>(this IBufferWriter<byte> writer, in T value)
            where T : unmanaged
        {
            var size = sizeof(T);
            var span = writer.GetSpan(size).Slice(0, size);
            Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(span), value);
        }

        public static void Write<T>(this IBufferWriter<byte> writer, in T value)
            where T : IWritable<T>
        {
            value.Write(writer);
        }

    }
}
