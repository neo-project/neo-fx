using DevHawk.Buffers;
using NeoFx.Storage;
using System;
using System.Collections.Immutable;

namespace NeoFx.P2P.Messages
{
    // used for getblocks and getheaders messages
    public readonly struct HashListPayload : IPayload<HashListPayload>
    {
        public readonly ImmutableArray<UInt256> HashStart;
        public readonly UInt256 HashStop;

        public HashListPayload(in UInt256 hashStart, in UInt256 hashStop = default)
        {
            HashStart = ImmutableArray.Create(hashStart);
            HashStop = hashStop == default ? UInt256.Zero : hashStop;
        }

        public HashListPayload(in ImmutableArray<UInt256> hashStart, in UInt256 hashStop)
        {
            HashStart = hashStart;
            HashStop = hashStop;
        }

        public int Size => HashStart.GetVarSize(UInt256.Size) + UInt256.Size;

        public static bool TryRead(ref BufferReader<byte> reader, out HashListPayload payload)
        {
            if (reader.TryReadVarArray<UInt256, UInt256.Factory>(out var hashStart)
                && UInt256.TryRead(ref reader, out var hashStop))
            {
                payload = new HashListPayload(hashStart, hashStop);
                return true;
            }

            payload = default;
            return false;
        }

        public void WriteTo(ref BufferWriter<byte> writer)
        {
            writer.WriteVarArray(HashStart);
            HashStop.WriteTo(ref writer);
        }
    }
}
