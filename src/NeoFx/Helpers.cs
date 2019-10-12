using NeoFx.Models;
using NeoFx.Storage;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;

namespace NeoFx
{
    public static class Helpers
    {
        private static readonly Lazy<SHA256> _sha256 = new Lazy<SHA256>(() => SHA256.Create());
        private static readonly Lazy<RIPEMD160> _ripemd160 = new Lazy<RIPEMD160>(() => RIPEMD160.Create());

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

        public static bool TryHash160(ReadOnlySpan<byte> message, Span<byte> hash)
        {
            Span<byte> tempBuffer = stackalloc byte[32];
            if (_sha256.Value.TryComputeHash(message, tempBuffer, out var written1)
                && _ripemd160.Value.TryComputeHash(tempBuffer, hash, out var written2))
            {
                Debug.Assert(written1 == 32 && written2 == 20);
                return true;
            }
            return false;
        }

        public static bool TryWriteHashData(in Transaction tx, Span<byte> span, out int bytesWritten)
        {
            var writer = new SpanWriter<byte>(span);
            if (writer.TryWrite((byte)tx.Type)
                && writer.TryWrite(tx.Version)
                && writer.TryWrite(tx.TransactionData.Span)
                && writer.TryWriteVarArray(tx.Attributes, BinaryFormat.TryWrite)
                && writer.TryWriteVarArray(tx.Inputs, BinaryFormat.TryWrite)
                && writer.TryWriteVarArray(tx.Outputs, BinaryFormat.TryWrite))
            {
                bytesWritten = writer.Contents.Length;
                return true;
            }

            bytesWritten = default;
            return false;
        }

        public static bool TryHash(in Transaction tx, out UInt256 hash)
        {
            using (var memBlock = MemoryPool<byte>.Shared.Rent(tx.GetSize()))
            {
                Span<byte> hashBuffer = stackalloc byte[32];

                if (TryWriteHashData(tx, memBlock.Memory.Span, out var bytesWritten)
                    && TryHash256(memBlock.Memory.Span.Slice(0, bytesWritten), hashBuffer))
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
