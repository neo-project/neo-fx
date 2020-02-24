using System;
using System.Buffers;
using System.Diagnostics;
using DevHawk.Buffers;
using NeoFx.Models;
using Newtonsoft.Json;

namespace NeoFx.RPC.Converters
{
    public class BlockConverter : JsonConverter<Block>
    {
        private static bool TryReadBlock(ref BufferReader<byte> reader, out Block block)
        {
            if (Block.TryRead(ref reader, out block))
            {
                Debug.Assert(reader.Remaining == 0);
                return true;
            }

            block = default;
            return false;
        }

        public override Block ReadJson(JsonReader reader, Type objectType, Block existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            
            if (reader.TryReadHexToken(TryReadBlock, out Block block))
            {
                return block;
            }

            throw new InvalidOperationException();
        }

        public override void WriteJson(JsonWriter writer, Block value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
