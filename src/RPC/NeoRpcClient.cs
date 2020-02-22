using System;
using System.Net.Http;
using System.Threading.Tasks;
using NeoFx.RPC.Converters;
using StreamJsonRpc;

namespace NeoFx.RPC
{
    using Version = NeoFx.RPC.Models.Version;

    public class NeoRpcClient
    {
        private readonly JsonRpc jsonRpc;

        public NeoRpcClient(Uri uri, HttpClient? httpClient = null)
        {
            var formatter = new JsonMessageFormatter();
            formatter.JsonSerializer.Converters.Add(new UInt256Converter());

            var messageHandler = new HttpClientMessageHandler(httpClient ?? new HttpClient(), uri, formatter);
            jsonRpc = new JsonRpc(messageHandler);
            jsonRpc.StartListening();
        }

        public Task<uint> GetLatestBlockIndexAsync()
        {
            return jsonRpc.InvokeAsync<uint>("getblockcount");
        }

        public Task<Version> GetVersionAsync()
        {
            return jsonRpc.InvokeAsync<Version>("getversion");
        }

        public Task<UInt256> GetBlockHashAsync(uint index)
        {
            return jsonRpc.InvokeAsync<UInt256>("getblockhash", index);
        }

    }
}
