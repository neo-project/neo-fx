using System;
using NeoFx.Models;
using Newtonsoft.Json;

namespace NeoFx.RPC.Converters
{
    public class TransactionConverter : JsonConverter<Transaction>
    {
        public override Transaction ReadJson(JsonReader reader, Type objectType, Transaction existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TryReadHexToken<Transaction>(Transaction.TryRead, out var tx))
            {
                return tx;
            }

            throw new InvalidOperationException();
        }

        public override void WriteJson(JsonWriter writer, Transaction value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
