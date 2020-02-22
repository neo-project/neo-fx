using DevHawk.Buffers;
using DevHawk.Security.Cryptography;
using NeoFx.Models;
using NeoFx.Storage;
using SimpleBase;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace NeoFx
{
    public static class HashHelpers
    {
        public static bool TryBase58CheckDecode(ReadOnlySpan<char> input, Span<byte> output, out int bytesWritten)
        {
            Span<byte> checksum = stackalloc byte[UInt256.Size];

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
            Span<byte> checksum = stackalloc byte[UInt256.Size];
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
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var segment in sequence)
            {
                hasher.AppendData(segment.Span);
            }

            Span<byte> tempBuffer = stackalloc byte[UInt256.Size];
            if (hasher.TryGetHashAndReset(tempBuffer, out var written1))
            {
                hasher.AppendData(tempBuffer);
                if (hasher.TryGetHashAndReset(hash, out var written2))
                {
                    Debug.Assert(written1 == UInt256.Size && written2 == UInt256.Size);
                    return true;
                }
            }

            return false;
        }

        public static bool TryHash256(ReadOnlySpan<byte> message, Span<byte> hash)
        {
            using var sha256 = SHA256.Create();
            Span<byte> tempBuffer = stackalloc byte[UInt256.Size];
            if (sha256.TryComputeHash(message, tempBuffer, out var written1)
                && sha256.TryComputeHash(tempBuffer, hash, out var written2))
            {
                Debug.Assert(written1 == UInt256.Size && written2 == UInt256.Size);
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

            Span<byte> tempBuffer = stackalloc byte[UInt256.Size];
            using var ripemd160 = new RIPEMD160();
            if (hasher.TryGetHashAndReset(tempBuffer, out var written1)
                && ripemd160.TryComputeHash(tempBuffer, hash, out var written2))
            {
                Debug.Assert(written1 == UInt256.Size && written2 == UInt160.Size);
                return true;
            }

            return false;
        }

        public static bool TryHash160(ReadOnlySpan<byte> message, Span<byte> hash)
        {
            Span<byte> tempBuffer = stackalloc byte[UInt256.Size];
            using var sha256 = SHA256.Create();
            using var ripemd160 = new RIPEMD160();
            if (sha256.TryComputeHash(message, tempBuffer, out var written1)
                && ripemd160.TryComputeHash(tempBuffer, hash, out var written2))
            {
                Debug.Assert(written1 == UInt256.Size && written2 == UInt160.Size);
                return true;
            }
            return false;
        }

        public static bool TryCalculateHash(this Transaction tx, Span<byte> hashBuffer)
        {
            var txSize = 2 + tx.GetTransactionDataSize() +
                +tx.Inputs.GetVarSize(CoinReference.Size)
                + tx.Outputs.GetVarSize(TransactionOutput.Size)
                + tx.Attributes.GetVarSize(a => a.Size);

            var buffer = new ArrayBufferWriter<byte>(txSize);
            var writer = new BufferWriter<byte>(buffer);

            writer.Write((byte)tx.GetTransactionType());
            writer.Write(tx.Version);
            tx.WriteTransactionData(ref writer);
            writer.WriteVarArray(tx.Attributes);
            writer.WriteVarArray(tx.Inputs);
            writer.WriteVarArray(tx.Outputs);
            writer.Commit();

            return TryHash256(buffer.WrittenSpan, hashBuffer);
        }

        public static UInt256 CalculateHash(this Transaction tx)
        {
            Span<byte> hashBuffer = stackalloc byte[UInt256.Size];
            if (TryCalculateHash(tx, hashBuffer))
            {
                return new UInt256(hashBuffer);
            }

            throw new ArgumentException(nameof(tx));
        }

        public static bool TryCalculateHash(in this BlockHeader header, Span<byte> hashBuffer)
        {
            var buffer = new ArrayBufferWriter<byte>(BlockHeader.ConstSize);
            var writer = new BufferWriter<byte>(buffer);

            writer.WriteLittleEndian(header.Version);
            header.PreviousHash.WriteTo(ref writer);
            header.MerkleRoot.WriteTo(ref writer);
            writer.WriteLittleEndian((uint)header.Timestamp.ToUnixTimeSeconds());
            writer.WriteLittleEndian(header.Index);
            writer.WriteLittleEndian(header.ConsensusData);
            header.NextConsensus.WriteTo(ref writer);
            writer.Commit();

            return TryHash256(buffer.WrittenSpan, hashBuffer);
        }

        public static UInt256 CalculateHash(in this BlockHeader header)
        {
            Span<byte> hashBuffer = stackalloc byte[UInt256.Size];
            if (TryCalculateHash(header, hashBuffer))
            {
                return new UInt256(hashBuffer);
            }

            throw new ArgumentException(nameof(header));
        }

        public static UInt256 CalculateHash(in this Block block) => block.Header.CalculateHash();

        public static bool TryInteropMethodHash(string methodName, out uint value)
        {
            Span<byte> asciiMethodName = stackalloc byte[(Encoding.ASCII.GetByteCount(methodName))];
            var getBytesWritten = Encoding.ASCII.GetBytes(methodName, asciiMethodName);
            Debug.Assert(getBytesWritten == asciiMethodName.Length);

            return TryInteropMethodHash(asciiMethodName, out value);
        }

        public static bool TryInteropMethodHash(Span<byte> asciiMethodName, out uint value)
        {
            using var sha256 = SHA256.Create();
            Span<byte> hashBuffer = stackalloc byte[UInt256.Size];
            if (sha256.TryComputeHash(asciiMethodName, hashBuffer, out var hashBytesWritten))
            {
                Debug.Assert(hashBytesWritten == hashBuffer.Length);
                value = BitConverter.ToUInt32(hashBuffer.Slice(0, sizeof(uint)));
                return true;
            }

            value = default;
            return false;
        }

        public static UInt160 CalculateScriptHash(this ReadOnlySpan<byte> span)
        {
            if (TryCalculateScriptHash(span, out var value))
            {
                return value;
            }

            throw new ArgumentException(nameof(span));
        }

        public static bool TryCalculateScriptHash(this ReadOnlySpan<byte> span, out UInt160 hash)
        {
            hash = default;
            var hashSpan = MemoryMarshal.CreateSpan(ref hash, 1);
            var hashBuffer = MemoryMarshal.Cast<UInt160, byte>(hashSpan);
            Debug.Assert(hashBuffer.Length >= UInt160.Size);
            return TryHash160(span, hashBuffer);
        }
    }
}
