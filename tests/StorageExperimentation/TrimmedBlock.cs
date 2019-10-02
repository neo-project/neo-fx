using NeoFx.Models;
using RocksDbSharp;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace StorageExperimentation
{

    readonly struct TrimmedBlock
    {
        public readonly BlockHeader Header;
        public readonly ReadOnlyMemory<UInt256> Hashes;

        public TrimmedBlock(BlockHeader header, ReadOnlyMemory<UInt256> hashes)
        {
            Header = header;
            Hashes = hashes;
        }

        public static bool TryRead(ref SequenceReader<byte> reader, out TrimmedBlock value)
        {
            if (BlockHeader.TryRead(ref reader, out var header)
                && reader.TryReadVarArray<UInt256>(UInt256.TryRead, out var hashes))
            {
                value = new TrimmedBlock(header, hashes);
                return true;
            }

            value = default;
            return false;
        }
    }
}
