using DevHawk.Buffers;
using NeoFx.Storage;
using System;
using System.Buffers;
using System.Numerics;

namespace NeoFx
{
    public static class ScriptBuilderHelpers
    {
        public static void EmitPush(this ref BufferWriter<byte> buffer, ReadOnlySpan<byte> span)
        {
            if (span.Length < OpCode.PUSHBYTES75)
            {
                buffer.Write((byte)span.Length);
                buffer.Write(span);
            }
            else if (span.Length < 0x100)
            {
                buffer.Write(OpCode.PUSHDATA1);
                buffer.Write((byte)span.Length);
                buffer.Write(span);

            }
            else if (span.Length < 0x10000)
            {
                buffer.Write(OpCode.PUSHDATA2);
                buffer.WriteLittleEndian((ushort)span.Length);
                buffer.Write(span);

            }
            else
            {
                buffer.Write(OpCode.PUSHDATA4);
                buffer.WriteLittleEndian(span.Length);
                buffer.Write(span);
            }
        }

        public static void EmitPush(this ref BufferWriter<byte> buffer, BigInteger number)
        {
            if (number == -1)
            {
                buffer.Write(OpCode.PUSHM1);
            }
            else if (number == 0)
            {
                buffer.Write(OpCode.PUSH0);
            }
            else if (number > 0 && number <= 16)
            {
                buffer.Write((byte)(0x50 + (byte)number)); //PUSH1 to PUSH16
            }
            else
            {
                var byteCount = number.GetByteCount();
                using var owner = MemoryPool<byte>.Shared.Rent(byteCount);
                Span<byte> numberBuffer = owner.Memory.Slice(0, byteCount).Span;
                if (number.TryWriteBytes(numberBuffer, out var _))
                {
                    buffer.EmitPush(numberBuffer);
                }
                else
                {
                    throw new ArgumentException(nameof(number));
                }
            }
        }

        // public static void EmitPush(this ref BufferWriter<byte> buffer, EncodedPublicKey publicKey)
        // {
        //     if (publicKey.TryCompress(out var compressedKey))
        //     {
        //         buffer.EmitPush(compressedKey.Key.AsSpan());
        //     }
        //     else
        //     {
        //         throw new ArgumentException(nameof(publicKey));
        //     }
        // }

        public static void EmitOpCode(this ref BufferWriter<byte> buffer, byte opCode, ReadOnlySpan<byte> arg = default)
        {
            buffer.Write(opCode);
            if (!arg.IsEmpty)
            {
                buffer.Write(arg);
            }
        }
    }
}
