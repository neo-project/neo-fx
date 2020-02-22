using System;
using System.Net.Http;
using System.Threading.Tasks;
using StreamJsonRpc;

namespace NeoFx.RPC
{
    public class NeoRpcClient
    {
        private readonly JsonRpc jsonRpc;

        public NeoRpcClient(Uri uri, HttpClient? httpClient = null)
        {
            var messageHandler = new HttpClientMessageHandler(httpClient ?? new HttpClient(), uri);
            jsonRpc = new JsonRpc(messageHandler);
            jsonRpc.StartListening();
        }

        public Task<uint> GetLatestBlockIndexAsync()
        {
            return jsonRpc.InvokeAsync<uint>("getblockcount");
        }

    }
}
