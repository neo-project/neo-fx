using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace NeoFx.Storage
{
    public readonly struct StorageKey
    {
        public readonly UInt160 ScriptHash;
        public readonly ReadOnlyMemory<byte> Key;

        public int Size => UInt160.Size + (((Key.Length / BlockSize) + 1) * (BlockSize + 1));

        public StorageKey(UInt160 scriptHash, ReadOnlyMemory<byte> key)
        {
            ScriptHash = scriptHash;
            Key = key;
        }

        // StorageKey.Key uses an atypical storage pattern relative to other models in NEO.
        // The byte array is written in blocks of 16 bytes followed by a byte indicating how many
        // bytes of the previous block were padding. Only the last block of 16 is allowed to have
        // padding read blocks of 16 (plus 1 padding indication byte) until padding indication byte
        // is greater than zero.

        private const int BlockSize = 16;

        public static bool TryRead(ReadOnlyMemory<byte> memory, out StorageKey value)
        {
            if (UInt160.TryRead(memory.Span, out var scriptHash))
            {
                memory = memory.Slice(UInt160.Size);

                Debug.Assert((memory.Length % (BlockSize + 1)) == 0);

                var memoryBlocks = new List<ReadOnlyMemory<byte>>(memory.Length / (BlockSize + 1));

                while (true)
                {
                    if (memory.Length < BlockSize + 1)
                    {
                        value = default;
                        return false;
                    }

                    var padding = memory.Span[BlockSize];
                    if (padding > 0)
                    {
                        Debug.Assert(memory.Length == BlockSize + 1);
                        if (padding < BlockSize)
                        {
                            memoryBlocks.Add(memory.Slice(0, BlockSize - padding));
                        }
                        break;
                    }
                    else
                    {
                        memoryBlocks.Add(memory.Slice(0, BlockSize));
                        memory = memory.Slice(BlockSize + 1);
                    }
                }

                Debug.Assert(memoryBlocks.Count > 0);

                // if there is only a single memory block, pass it directly to the storage key ctor
                if (memoryBlocks.Count == 1)
                {
                    value = new StorageKey(scriptHash, memoryBlocks[0]);
                    return true;
                }

                Debug.Assert(memoryBlocks.Count > 1);

                // if there is more than one memory block, make a pass thru the list to calculate the 
                // total size of the buffer.
                var size = 0;
                for (int i = 0; i < memoryBlocks.Count; i++)
                {
                    size += memoryBlocks[i].Length;
                }

                // after calculating the size of the buffer, make a second pass thru the list copying
                // the contents of each memory block into the single contigious buffer.
                var buffer = new byte[size];
                var position = 0;
                for (int i = 0; i < memoryBlocks.Count; i++)
                {
                    var block = memoryBlocks[i];
                    block.CopyTo(buffer.AsMemory().Slice(position, block.Length));
                    position += block.Length;
                }

                value = new StorageKey(scriptHash, buffer);
                return true;
            }

            value = default;
            return false;
        }

        public bool TryWrite(Span<byte> span)
        {
            if (span.Length >= Size && ScriptHash.TryWrite(span))
            {
                span = span.Slice(UInt160.Size);
                var keySpan = Key.Span;

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

                return true;
            }

            return false;
        }
    }
}
