using System;
using System.Buffers;
using System.Diagnostics;
using DevHawk.Buffers;
using NeoFx.Models;
using Newtonsoft.Json;

namespace NeoFx.RPC.Converters
{
    public class BlockHeaderConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
            => objectType.Equals(typeof(BlockHeader));

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String
                && TryParseBlockHeader((string)reader.Value, out var header))
            {
                return header;
            }

            throw new InvalidOperationException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        private static bool TryParseBlockHeader(string hex, out BlockHeader header)
        {
            using var memoryOwner = MemoryPool<byte>.Shared.Rent(hex.Length >> 1);

            if (hex.TryConvertHexString(memoryOwner.Memory.Span, out var bytesWritten))
            {
                var reader = new BufferReader<byte>(memoryOwner.Memory.Span.Slice(0, hex.Length >> 1));
                if (BlockHeader.TryRead(ref reader, out header)
                    && reader.TryRead(out byte txCount)
                    && txCount == 0)
                {
                    Debug.Assert(reader.Remaining == 0);
                    return true;
                }
            }

            header = default;
            return false;
        }
    }
}
