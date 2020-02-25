using System;
using System.Net.Http;
using System.Threading.Tasks;
using NeoFx.Models;
using NeoFx.RPC.Converters;
using NeoFx.RPC.Models;
using Newtonsoft.Json.Linq;
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
            formatter.JsonSerializer.Converters.Add(new AccountConverter());
            formatter.JsonSerializer.Converters.Add(new BlockConverter());
            formatter.JsonSerializer.Converters.Add(new BlockHeaderConverter());
            formatter.JsonSerializer.Converters.Add(new PeersConverter());
            formatter.JsonSerializer.Converters.Add(new TransactionConverter());
            formatter.JsonSerializer.Converters.Add(new UInt256Converter());
            formatter.JsonSerializer.Converters.Add(new ValidatorConverter());

            var messageHandler = new HttpClientMessageHandler(httpClient ?? new HttpClient(), uri, formatter);
            jsonRpc = new JsonRpc(messageHandler);
            jsonRpc.StartListening();
        }

        public Task<Account> GetAccountStateAsync(string address)
        {
            return jsonRpc.InvokeAsync<Account>("getaccountstate", address);
        }

        public Task<Account> GetAccountStateAsync(UInt160 scriptHash)
        {
            return GetAccountStateAsync(scriptHash.ToAddress());
        }

        // getassetstate 

        public Task<UInt256> GetBestBlockHashAsync()
        {
            return jsonRpc.InvokeAsync<UInt256>("getbestblockhash");
        }

        public Task<Block> GetBlockAsync(uint index)
        {
            return jsonRpc.InvokeAsync<Block>("getblock", index);
        }

        public Task<Block> GetBlockAsync(UInt256 hash)
        {
            return jsonRpc.InvokeAsync<Block>("getblock", hash);
        }

        public Task<uint> GetBlockCountAsync()
        {
            return jsonRpc.InvokeAsync<uint>("getblockcount");
        }

        public Task<UInt256> GetBlockHashAsync(uint index)
        {
            return jsonRpc.InvokeAsync<UInt256>("getblockhash", index);
        }

        public Task<BlockHeader> GetBlockHeaderAsync(uint index)
        {
            return jsonRpc.InvokeAsync<BlockHeader>("getblockheader", index);
        }

        public Task<BlockHeader> GetBlockHeaderAsync(UInt256 hash)
        {
            return jsonRpc.InvokeAsync<BlockHeader>("getblockheader", hash);
        }

        public Task<long> GetBlockSysFeeAsync(uint index)
        {
            return jsonRpc.InvokeAsync<long>("getblocksysfee", index);
        }

        public Task<int> GetConnectionCountAsync()
        {
            return jsonRpc.InvokeAsync<int>("getconnectioncount");
        }

        // getcontractstate
        
        public Task<Peers> GetPeersAsync()
        {
            return jsonRpc.InvokeAsync<Peers>("getpeers");
        }

        // getrawmempool

        public Task<Transaction> GetRawTransactionAsync(UInt256 hash)
        {
            return jsonRpc.InvokeAsync<Transaction>("getrawtransaction", hash);
        }

        // getstorage
        // gettransactionheight
        // gettxout

        public Task<Validator[]> GetValidatorsAsync()
        {
            return jsonRpc.InvokeAsync<Validator[]>("getvalidators");
        }

        public Task<Version> GetVersionAsync()
        {
            return jsonRpc.InvokeAsync<Version>("getversion");
        }

        // invoke
        // invokefunction
        // invokescript
        // listplugins
        // sendrawtransaction
        // submitblock
        
        public async Task<bool> ValidateAddressAsync(string address)
        {
            var json = await jsonRpc.InvokeAsync<JObject>("validateaddress", address);
            return json.Value<bool>("isvalid");
        }

        public Task<bool> ValidateAddressAsync(UInt160 scriptHash)
        {
            return ValidateAddressAsync(scriptHash.ToAddress());
        }
    }
}
