using System;
using System.Diagnostics;
using Newtonsoft.Json;

namespace NeoFx.RPC.Converters
{
    public class UInt256Converter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
            => objectType.Equals(typeof(UInt256));

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return reader.TokenType == JsonToken.String
                           ? UInt256.Parse((string)reader.Value)
                           : throw new InvalidOperationException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            Debug.Assert(value.GetType() == typeof(UInt256));

            writer.WriteValue(((UInt256)value).ToString());
        }
    }
}
