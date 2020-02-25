using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using NeoFx.RPC.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeoFx.RPC.Converters
{
    public class PeersConverter : JsonConverter<Peers>
    {
        IEnumerable<(IPAddress address, int port)> ParseAddressList(JToken token)
        {
            foreach (var item in token)
            {
                var address = IPAddress.Parse(item.Value<string>("address"));
                var port = int.Parse(item.Value<string>("port"));
                yield return (address, port);
            }
        }
        
        public override Peers ReadJson(JsonReader reader, Type objectType, Peers existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            Debug.Assert(reader.TokenType == JsonToken.StartObject);

            // TODO: read w/o loading full JObject
            var result = JObject.ReadFrom(reader);
            var unconnected = ParseAddressList(result["unconnected"]);
            var connected = ParseAddressList(result["connected"]);

            return new Peers(unconnected, connected);
        }
 
        public override void WriteJson(JsonWriter writer, Peers value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
