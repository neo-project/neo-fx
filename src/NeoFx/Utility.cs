using NeoFx.Models;
using NeoFx.Storage;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace NeoFx
{
    public static class Utility
    {
        private static Lazy<SHA256> _sha256 = new Lazy<SHA256>(() => SHA256.Create());

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

        public static int GetVarSize(this ReadOnlyMemory<byte> value)
        {
            return value.Span.GetVarSize();
        }

        public static int GetVarSize(this ReadOnlySpan<byte> value)
        {
            return GetVarSize((ulong)value.Length) + value.Length;
        }

        public static int GetVarSize(this string value)
        {
            int size = System.Text.Encoding.UTF8.GetByteCount(value);
            return GetVarSize((ulong)size) + size;
        }

        public static byte[] Base58CheckDecode(this string input)
        {
            var buffer = SimpleBase.Base58.Bitcoin.Decode(input);
            if (buffer.Length < 4) throw new FormatException();

            Span<byte> checksumPrime = stackalloc byte[32];
            if (_sha256.Value.TryComputeHash(buffer.Slice(0, buffer.Length - 4), checksumPrime, out var written))
            {
                Debug.Assert(written == 32);

                Span<byte> checksum = stackalloc byte[32];
                if (_sha256.Value.TryComputeHash(checksumPrime, checksum, out written))
                {
                    Debug.Assert(written == 32);

                    if (buffer.Slice(buffer.Length - 4).SequenceEqual(checksum.Slice(0, 4)))
                    {
                        return buffer.Slice(0, buffer.Length - 4).ToArray();
                    }
                }
            }
            throw new FormatException();
        }

        public static UInt160 ToScriptHash(this string address)
        {
            byte[] data = address.Base58CheckDecode();
            if (data.Length != 21)
                throw new FormatException();
            return new UInt160(data.AsSpan().Slice(1));
        }

        public static bool TryHash256(ReadOnlySpan<byte> message, Span<byte> hash)
        {
            Span<byte> tempBuffer = stackalloc byte[32];
            if (_sha256.Value.TryComputeHash(message, tempBuffer, out var written1)
                && _sha256.Value.TryComputeHash(tempBuffer, hash, out var written2))
            {
                Debug.Assert(written1 == 32 && written2 == 32);

                return true;
            }

            return false;
        }

        public static bool TryHash(Transaction tx, out UInt256 hash)
        {
            using (var memBlock = MemoryPool<byte>.Shared.Rent(2048))
            {
                var writer = new SpanWriter<byte>(memBlock.Memory.Span);
                Span<byte> hashBuffer = stackalloc byte[32];

                if (writer.TryWrite((byte)tx.Type)
                    && writer.TryWrite(tx.Version)
                    && writer.TryWrite(tx.TransactionData.Span)
                    && writer.TryWriteVarArray(tx.Attributes, BinaryWriter.TryWrite)
                    && writer.TryWriteVarArray(tx.Inputs, BinaryWriter.TryWrite)
                    && writer.TryWriteVarArray(tx.Outputs, BinaryWriter.TryWrite)
                    && TryHash256(writer.Contents, hashBuffer))
                {
                    hash = new UInt256(hashBuffer);
                    return true;
                }
            }

            hash = default;
            return false;
        }
    }
}
