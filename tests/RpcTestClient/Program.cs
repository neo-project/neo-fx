using System;
using System.Threading.Tasks;
using NeoFx.RPC;

namespace RpcTestClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var uri = new Uri("http://seed1.ngd.network:20332");
            var client = new NeoRpcClient(uri);
            var index = await client.GetLatestBlockIndexAsync();
            Console.WriteLine(index);
        }
    }
}
