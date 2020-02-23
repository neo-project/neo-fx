using System;
using System.Buffers;
using System.Diagnostics;
using DevHawk.Buffers;
using NeoFx.Models;
using Newtonsoft.Json;

namespace NeoFx.RPC.Converters
{
    public class BlockConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
            => objectType.Equals(typeof(Block));

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String
                && TryParseBlock((string)reader.Value, out var block))
            {
                return block;
            }

            throw new InvalidOperationException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        private static bool TryParseBlock(string hex, out Block block)
        {
            using var memoryOwner = MemoryPool<byte>.Shared.Rent(hex.Length >> 1);
            if (hex.TryConvertHexString(memoryOwner.Memory.Span, out var bytesWritten))
            {
                Debug.Assert(bytesWritten == hex.Length >> 1);
                var reader = new BufferReader<byte>(memoryOwner.Memory.Span.Slice(0, bytesWritten));
                if (Block.TryRead(ref reader, out block))
                {
                    Debug.Assert(reader.Remaining == 0);
                    return true;
                }
            }

            block = default;
            return false;
        }
    }
}
