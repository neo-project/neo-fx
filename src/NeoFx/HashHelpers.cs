using DevHawk.Buffers;
using NeoFx.Models;
using NeoFx.Storage;
using SimpleBase;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace NeoFx
{
    public static class HashHelpers
    {
        public const int Hash256Size = 32;
        public const int Hash160Size = 20;

        private static readonly Lazy<SHA256> _sha256 = new Lazy<SHA256>(() => SHA256.Create());
        private static readonly Lazy<RIPEMD160> _ripemd160 = new Lazy<RIPEMD160>(() => new RIPEMD160());

        public static bool TryBase58CheckDecode(ReadOnlySpan<char> input, Span<byte> output, out int bytesWritten)
        {
            Span<byte> checksum = stackalloc byte[Hash256Size];

            var bufferSize = Base58.Bitcoin.GetSafeByteCountForDecoding(input);
            if (bufferSize >= 4)
            {
                Span<byte> buffer = stackalloc byte[bufferSize];
                if (Base58.Bitcoin.TryDecode(input, buffer, out var written)
                    && TryHash256(buffer.Slice(0, written - 4), checksum)
                    && buffer.Slice(written - 4, 4).SequenceEqual(checksum.Slice(0, 4))
                    && buffer.Slice(0, written - 4).TryCopyTo(output))
                {
                    bytesWritten = written - 4;
                    return true;
                }
            }

            bytesWritten = default;
            return false;
        }

        public static bool TryBase58CheckEncode(ReadOnlySpan<byte> input, [NotNullWhen(true)] out string? output)
        {
            Span<byte> checksum = stackalloc byte[Hash256Size];
            Span<byte> encodeBuffer = stackalloc byte[input.Length + 4];

            if (TryHash256(input, checksum)
                && input.TryCopyTo(encodeBuffer.Slice(0, input.Length))
                && checksum.Slice(0, 4).TryCopyTo(encodeBuffer.Slice(input.Length, 4)))
            {
                output = Base58.Bitcoin.Encode(encodeBuffer);
                return true;
            }

            output = default;
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

        public static string ToAddress(this UInt160 scriptHash, byte addressVersion = 0x17)
        {
            Span<byte> buffer = stackalloc byte[21];
            buffer[0] = addressVersion;
            if (scriptHash.TryWrite(buffer.Slice(1))
                && TryBase58CheckEncode(buffer, out var address))
            {
                return address;
            }

            throw new ArgumentException(nameof(scriptHash));
        }

        public static bool TryHash256(ReadOnlySequence<byte> sequence, Span<byte> hash)
        {
            var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var segment in sequence)
            {
                hasher.AppendData(segment.Span);
            }

            Span<byte> tempBuffer = stackalloc byte[Hash256Size];
            if (hasher.TryGetHashAndReset(tempBuffer, out var written1)
                && _sha256.Value.TryComputeHash(tempBuffer, hash, out var written2))
            {
                Debug.Assert(written1 == Hash256Size && written2 == Hash256Size);
                return true;
            }

            return false;
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

        public static bool TryHash160(ReadOnlySequence<byte> sequence, Span<byte> hash)
        {
            var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var segment in sequence)
            {
                hasher.AppendData(segment.Span);
            }

            Span<byte> tempBuffer = stackalloc byte[Hash256Size];
            if (hasher.TryGetHashAndReset(tempBuffer, out var written1)
                && _ripemd160.Value.TryComputeHash(tempBuffer, hash, out var written2))
            {
                Debug.Assert(written1 == Hash256Size && written2 == Hash160Size);
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
                Debug.Assert(written1 == Hash256Size && written2 == Hash160Size);
                return true;
            }
            return false;
        }

        public static void WriteHashData(ref BufferWriter<byte> writer, Transaction tx)
        {
            tx.WriteTransactionData(ref writer);
            writer.WriteVarArray(tx.Attributes);
            writer.WriteVarArray(tx.Inputs);
            writer.WriteVarArray(tx.Outputs);
        }

        public static bool TryHash(Transaction tx, out UInt256 hash)
        {
            var buffer = new ArrayBufferWriter<byte>(1024);
            var writer = new BufferWriter<byte>(buffer);

            WriteHashData(ref writer, tx);

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
