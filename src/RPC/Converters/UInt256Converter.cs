using System;
using System.Diagnostics;
using Newtonsoft.Json;

namespace NeoFx.RPC.Converters
{
    public class UInt256Converter : JsonConverter<UInt256>
    {
        public override UInt256 ReadJson(JsonReader reader, Type objectType, UInt256 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return reader.TokenType == JsonToken.String
                           ? UInt256.Parse((string)reader.Value)
                           : throw new InvalidOperationException();
        }

        public override void WriteJson(JsonWriter writer, UInt256 value, JsonSerializer serializer)
        {
            writer.WriteValue(((UInt256)value).ToString());
        }
    }
}
