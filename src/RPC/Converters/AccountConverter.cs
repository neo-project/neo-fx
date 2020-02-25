using System;
using System.Diagnostics;
using System.Linq;
using NeoFx.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Immutable;

namespace NeoFx.RPC.Converters
{
    public class AccountConverter : JsonConverter<Account>
    {
        public override Account ReadJson(JsonReader reader, Type objectType, Account existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            Debug.Assert(reader.TokenType == JsonToken.StartObject);

            // TODO: read w/o loading full JObject
            var result = JObject.ReadFrom(reader);

            var scriptHash = result.Value<string>("script_hash").ToScriptHash();
            var isFrozen = result.Value<bool>("frozen");
            var votes = result["votes"]
                .Select(t => EncodedPublicKey.Parse(t.Value<string>()))
                .ToImmutableArray();
            var balances = result["balances"].ToImmutableDictionary(
                t => UInt256.Parse(t.Value<string>("asset")),
                t => Fixed8.Parse(t.Value<string>("value")));

            return new Account(scriptHash, isFrozen, votes, balances);
        }

        public override void WriteJson(JsonWriter writer, Account value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
