using System;
using System.Net.Http;
using System.Threading.Tasks;
using NeoFx.Models;
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
            formatter.JsonSerializer.Converters.Add(new BlockHeaderConverter());
            formatter.JsonSerializer.Converters.Add(new BlockConverter());

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

        public Task<UInt256> GetBestBlockHashAsync()
        {
            return jsonRpc.InvokeAsync<UInt256>("getbestblockhash");
        }

        public Task<BlockHeader> GetBlockHeaderAsync(uint index)
        {
            return jsonRpc.InvokeAsync<BlockHeader>("getblockheader", index);
        }

        public Task<BlockHeader> GetBlockHeaderAsync(UInt256 hash)
        {
            return jsonRpc.InvokeAsync<BlockHeader>("getblockheader", hash);
        }

        public Task<Block> GetBlockAsync(uint index)
        {
            return jsonRpc.InvokeAsync<Block>("getblock", index);
        }

        public Task<Block> GetBlockAsync(UInt256 hash)
        {
            return jsonRpc.InvokeAsync<Block>("getblock", hash);
        }
    }
}
