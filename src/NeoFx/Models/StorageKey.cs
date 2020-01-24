using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NeoFx.Models
{
    public readonly struct StorageKey
    {
        public readonly UInt160 ScriptHash;
        public readonly ImmutableArray<byte> Key;

        public const int BlockSize = 16;

        public int Size => UInt160.Size + (((Key.Length / BlockSize) + 1) * (BlockSize + 1));

        public StorageKey(UInt160 scriptHash, ImmutableArray<byte> key)
        {
            ScriptHash = scriptHash;
            Key = key;
        }

        public static bool TryReadBytes(ReadOnlySpan<byte> span, out StorageKey value)
        {
            // StorageKey.Key uses an atypical storage pattern relative to other models in Neo.
            // The byte array is written in blocks of 16 bytes followed by a byte indicating how many
            // bytes of the previous block were padding. Only the last block of 16 is allowed to have
            // padding. Read blocks of 16 (plus 1 padding indication byte) until padding indication byte
            // is greater than zero.

            if (UInt160.TryRead(span, out var scriptHash))
            {
                span = span.Slice(UInt160.Size);
                var blockCount = span.Length / (BlockSize + 1);

                Debug.Assert((span.Length % (BlockSize + 1)) == 0);
                Debug.Assert(blockCount > 0);

                var padding = span[span.Length - 1];
                var bufferSize = (blockCount * BlockSize) - padding;
                var buffer = new byte[bufferSize];

                for (int i = 0; i < blockCount; i++)
                {
                    var src = span.Slice(
                        i * (BlockSize + 1),
                        BlockSize - ((i == blockCount - 1) ? padding : 0));
                    var dst = buffer.AsSpan().Slice(i * BlockSize);
                    src.CopyTo(dst);
                }

                value = new StorageKey(scriptHash, Unsafe.As<byte[], ImmutableArray<byte>>(ref buffer));
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

                while (keySpan.Length >= BlockSize)
                {
                    keySpan.Slice(0, BlockSize).CopyTo(span);
                    span[BlockSize] = 0;

                    keySpan = keySpan.Slice(BlockSize);
                    span = span.Slice(BlockSize + 1);
                }

                Debug.Assert(span.Length == BlockSize + 1);

                keySpan.CopyTo(span);
                span.Slice(keySpan.Length).Clear();
                span[BlockSize] = (byte)(BlockSize - keySpan.Length);

                bytesWritten = Size;
                return true;
            }

            bytesWritten = default;
            return false;
        }

    }
}
