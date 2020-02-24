using System;
using System.Buffers;
using System.Diagnostics;
using DevHawk.Buffers;
using NeoFx.Models;
using Newtonsoft.Json;

namespace NeoFx.RPC.Converters
{
    public class BlockHeaderConverter : JsonConverter<BlockHeader>
    {
        public override BlockHeader ReadJson(JsonReader reader, Type objectType, BlockHeader existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TryReadHexToken(TryReadBlockHeader, out BlockHeader header))
            {
                return header;
            }

            throw new InvalidOperationException();
        }

        private static bool TryReadBlockHeader(ref BufferReader<byte> reader, out BlockHeader header)
        {
            if (BlockHeader.TryRead(ref reader, out header)
                && reader.TryRead(out byte txCount)
                && txCount == 0)
            {
                Debug.Assert(reader.Remaining == 0);
                return true;
            }

            header = default;
            return false;
        }

        public override void WriteJson(JsonWriter writer, BlockHeader value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
