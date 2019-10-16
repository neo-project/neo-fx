using NeoFx.Models;
using NeoFx.Storage;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace NeoFx
{
    public static class HashHelpers
    {
        public const int Hash256Size = 32;
        public const int Hash160Size = 20;

        private static readonly Lazy<SHA256> _sha256 = new Lazy<SHA256>(() => SHA256.Create());
        private static readonly Lazy<RIPEMD160> _ripemd160 = new Lazy<RIPEMD160>(() => RIPEMD160.Create());

        public static int GetBase58CheckDecodeByteCount(ReadOnlySpan<char> input)
        {
            // TODO: rewrite this function when SimpleBase is updated
            var buffer = SimpleBase.Base58.Bitcoin.Decode(input);
            if (buffer.Length < 4) throw new FormatException();
            return buffer.Length - 4;
        }

        public static bool TryBase58CheckDecode(ReadOnlySpan<char> input, Span<byte> output, out int bytesWritten)
        {
            // TODO: rewrite this function when SimpleBase is updated
            Span<byte> checksum = stackalloc byte[Hash256Size];
            var buffer = SimpleBase.Base58.Bitcoin.Decode(input);
            if (buffer.Length >= 4
                && TryHash256(buffer.Slice(0, buffer.Length - 4), checksum)
                && buffer.Slice(buffer.Length - 4).SequenceEqual(checksum.Slice(0, 4))
                && buffer.Slice(0, buffer.Length - 4).TryCopyTo(output))
            {
                bytesWritten = buffer.Length - 4;
                return true;
            }

            bytesWritten = default;
            return false;
        }

        public static UInt160 ToScriptHash(this string address)
        {
            Span<byte> buffer = stackalloc byte[21];
            if (TryBase58CheckDecode(address, buffer, out var written)
                && written == 21)
            {
                return new UInt160(buffer.Slice(1));
            }

            throw new ArgumentException(nameof(address));
        }

        public static bool TryHash256(ReadOnlySpan<byte> message, Span<byte> hash)
        {
            Span<byte> tempBuffer = stackalloc byte[Hash256Size];
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
            Span<byte> tempBuffer = stackalloc byte[Hash256Size];
            if (_sha256.Value.TryComputeHash(message, tempBuffer, out var written1)
                && _ripemd160.Value.TryComputeHash(tempBuffer, hash, out var written2))
            {
                Debug.Assert(written1 == 32 && written2 == 20);
                return true;
            }
            return false;
        }

        public static void WriteHashData(in Transaction tx, IBufferWriter<byte> buffer)
        {
            buffer.WriteData(tx);
            buffer.WriteVarArray(tx.Attributes.Span, BinaryFormat.Write);
            buffer.WriteVarArray(tx.Inputs.Span, BinaryFormat.Write);
            buffer.WriteVarArray(tx.Outputs.Span, BinaryFormat.Write);
        }

        public static bool TryHash(in Transaction tx, out UInt256 hash)
        {
            var buffer = new ArrayBufferWriter<byte>(tx.GetSize());
            WriteHashData(tx, buffer);

            Span<byte> hashBuffer = stackalloc byte[Hash256Size];
            if (TryHash256(buffer.WrittenSpan, hashBuffer))
            {
                hash = new UInt256(hashBuffer);
                return true;
            }

            hash = default;
            return false;
        }
        
        public static bool TryInteropMethodHash(string methodName, out uint value)
        {
            Span<byte> asciiMethodName = stackalloc byte[(Encoding.ASCII.GetByteCount(methodName))];
            var getBytesWritten = Encoding.ASCII.GetBytes(methodName, asciiMethodName);
            Debug.Assert(getBytesWritten == asciiMethodName.Length);

            return TryInteropMethodHash(asciiMethodName, out value);
        }

        public static bool TryInteropMethodHash(Span<byte> asciiMethodName, out uint value)
        {
            Span<byte> hashBuffer = stackalloc byte[Hash256Size];
            if (_sha256.Value.TryComputeHash(asciiMethodName, hashBuffer, out var hashBytesWritten))
            {
                Debug.Assert(hashBytesWritten == hashBuffer.Length);
                value = BitConverter.ToUInt32(hashBuffer.Slice(0, sizeof(uint)));
                return true;
            }

            value = default;
            return false;
        }
    }
}
