using System;
using System.Diagnostics;
using NeoFx.Models;
using NeoFx.RPC.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeoFx.RPC.Converters
{
    public class ValidatorConverter : JsonConverter<Validator>
    {
        public override Validator ReadJson(JsonReader reader, Type objectType, Validator existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            Debug.Assert(reader.TokenType == JsonToken.StartObject);

            // TODO: read w/o loading full JObject
            var result = JObject.ReadFrom(reader);
            var key = EncodedPublicKey.Parse(result.Value<string>("publickey"));
            var votes = Fixed8.Parse(result.Value<string>("votes"));
            var active = result.Value<bool>("active");
            
            return new Validator(key, active, votes); 
        }

        public override void WriteJson(JsonWriter writer, Validator value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
