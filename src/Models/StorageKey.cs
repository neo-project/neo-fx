using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace NeoFx.Models
{
    public readonly struct StorageKey
    {
        public readonly UInt160 ScriptHash;
        public readonly ReadOnlyMemory<byte> Key;

        public StorageKey(UInt160 scriptHash, ReadOnlyMemory<byte> key)
        {
            ScriptHash = scriptHash;
            Key = key;
        }

        public static bool TryRead(ref SequenceReader<byte> reader, out StorageKey value)
        {
            const int BlockSize = 16;

            if (UInt160.TryRead(ref reader, out var scriptHash))
            {
                // StorageKey.Key uses an atypical storage pattern relative to other models in NEO.
                // The byte array is written in blocks of 16 bytes followed by a byte indicating how many
                // bytes of the previous block were padding. Only the last block of 16 is allowed to have
                // padding read blocks of 16 (plus 1 padding indication byte) until padding indication byte
                // is greater than zero.

                var memoryBlocks = new List<ReadOnlyMemory<byte>>();

                while (true)
                {
                    // read the block of 16 bytes + the padding indicator byte
                    if (reader.TryReadByteArray(BlockSize, out var memory)
                        && reader.TryRead(out var padding))
                    {
                        Debug.Assert(padding <= BlockSize);

                        memoryBlocks.Add(memory.Slice(0, BlockSize - padding));

                        if (padding > 0)
                        {
                            break;
                        }
                    }
                    else
                    {
                        value = default;
                        return false;
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

                value = new StorageKey(scriptHash, memoryBlocks[0]);
                return true;
            }

            value = default;
            return false;
        }
    }
}
