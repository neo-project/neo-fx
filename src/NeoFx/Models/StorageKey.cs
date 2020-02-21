using System;
using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DevHawk.Buffers;
using NeoFx.Storage;

namespace NeoFx.Models
{
    public readonly struct StorageKey
    {
        public readonly UInt160 ScriptHash;
        public readonly ImmutableArray<byte> Key;

        public const int BLOCK_SIZE = 16;

        public int Size => UInt160.Size + (((Key.Length / BLOCK_SIZE) + 1) * (BLOCK_SIZE + 1));

        public StorageKey(UInt160 scriptHash, ImmutableArray<byte> key)
        {
            ScriptHash = scriptHash;
            Key = key == default ? ImmutableArray.Create<byte>() : key;
        }

        // StorageKey.Key uses an atypical storage pattern relative to other models in Neo.
        // The byte array is written in blocks of BLOCK_SIZE (aka 16) bytes  followed by a byte 
        // indicating how many bytes of the previous block were padding. Only the last block is
        // allowed to have padding. Read blocks of BLOCK_SIZE + 1 until padding indication byte
        // is greater than zero.

        public static bool TryRead(ref BufferReader<byte> reader, out StorageKey value)
        {
            const int READ_BLOCK_SIZE = BLOCK_SIZE + 1;

            if (UInt160.TryRead(ref reader, out var scriptHash))
            {
                using var bufferOwner = MemoryPool<byte>.Shared.Rent(READ_BLOCK_SIZE);
                var buffer = bufferOwner.Memory.Slice(0, READ_BLOCK_SIZE).Span;
                var writer = new ArrayBufferWriter<byte>();

                while (true)
                {
                    if (!reader.TryCopyTo(buffer))
                    {
                        value = default;
                        return false;
                    }
                    reader.Advance(READ_BLOCK_SIZE);

                    var dataSize = BLOCK_SIZE - buffer[BLOCK_SIZE];
                    buffer.Slice(0, dataSize).CopyTo(writer.GetSpan(dataSize));
                    writer.Advance(dataSize);

                    if (dataSize < BLOCK_SIZE)
                    {
                        break;
                    }
                }

                // unfortunately, since we don't know a priori how many blocks there will be
                // or how much padding the last block will have, we have to make another copy
                // of the key array. However, we can use Unsafe.As to cast the mutable key array
                // into an ImmutableArray
                var keyArray = writer.WrittenSpan.ToArray();
                value = new StorageKey(scriptHash, Unsafe.As<byte[], ImmutableArray<byte>>(ref keyArray));

                return true;
            }

            value = default;
            return false;
        }

        public bool TryWrite(Span<byte> span, out int bytesWritten)
        {
            if (span.Length >= Size && ScriptHash.TryWrite(span))
            {
                span = span.Slice(UInt160.Size);
                var keySpan = Key.AsSpan();

                while (keySpan.Length >= BLOCK_SIZE)
                {
                    keySpan.Slice(0, BLOCK_SIZE).CopyTo(span);
                    span[BLOCK_SIZE] = 0;

                    keySpan = keySpan.Slice(BLOCK_SIZE);
                    span = span.Slice(BLOCK_SIZE + 1);
                }

                Debug.Assert(span.Length == BLOCK_SIZE + 1);

                keySpan.CopyTo(span);
                span.Slice(keySpan.Length).Clear();
                span[BLOCK_SIZE] = (byte)(BLOCK_SIZE - keySpan.Length);

                bytesWritten = Size;
                return true;
            }

            bytesWritten = default;
            return false;
        }
    }
}
