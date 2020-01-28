using System;
using System.Collections.Immutable;
using DevHawk.Buffers;
using NeoFx.Models;
using NeoFx.Storage;

namespace NeoFx.P2P.Messages
{
    public readonly struct HeadersPayload
    {
        public readonly ImmutableArray<BlockHeader> Headers;

        public HeadersPayload(ImmutableArray<BlockHeader> headers)
        {
            Headers = headers;
        }

        private readonly struct HeaderFactory : IFactoryReader<BlockHeader>
        {
            public bool TryReadItem(ref BufferReader<byte> reader, out BlockHeader header)
            {
                if (BlockHeader.TryRead(ref reader, out header)
                    && reader.TryReadVarInt(out var txCount)
                    && txCount == 0)
                {
                    return true;
                }

                header = default;
                return false;
            }
        }

        public static bool TryRead(ref BufferReader<byte> reader, out HeadersPayload payload)
        {
            if (reader.TryReadVarArray<BlockHeader, HeaderFactory>(out var headers))
            {
                payload = new HeadersPayload(headers);
                return true;
            }

            payload = default;
            return false;
        }
    }
}
