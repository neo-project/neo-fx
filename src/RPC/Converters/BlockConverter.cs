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
        public override Block ReadJson(JsonReader reader, Type objectType, Block existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
           
            if (reader.TryReadHexToken(Block.TryRead, out Block block))
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
