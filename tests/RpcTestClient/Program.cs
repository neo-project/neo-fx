using System;
using System.Threading.Tasks;
using NeoFx;
using NeoFx.RPC;

namespace RpcTestClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var uri = new Uri("http://seed6.ngd.network:20332");
            var client = new NeoRpcClient(uri);

            var index = await client.GetBlockCountAsync();
                Console.WriteLine(index);

            var version = await client.GetVersionAsync();
            Console.WriteLine($"{version.Nonce}-{version.Port}-{version.UserAgent}");

            var hash = await client.GetBlockHashAsync(0);
            Console.WriteLine(hash);

            var header = await client.GetBlockHeaderAsync(hash);
            Console.WriteLine($"{header.Index}-{header.Timestamp}");

            // var peers = await client.GetPeersAsync();
            // Console.WriteLine($"{peers.Connected.Length}-{peers.Unconnected.Length}");
            // Console.WriteLine("connected");
            // foreach (var peer in peers.Unconnected)
            // {
            //     Console.WriteLine($"\t{peer.address}:{peer.port}");
            // }
            // Console.WriteLine("unconnected");
            // foreach (var peer in peers.Unconnected)
            // {
            //     Console.WriteLine($"\t{peer.address}:{peer.port}");
            // }

            // Console.WriteLine("\nValidators");
            // foreach (var validator in await client.GetValidatorsAsync())
            // {
            //     Console.WriteLine($"\t{validator.PublicKey}");
            // }

            var valid = await client.ValidateAddressAsync("AQVh2pG732YvtNaxEGkQUei3YA4cvo7d2i");
            Console.WriteLine($"AQVh2pG732YvtNaxEGkQUei3YA4cvo7d2i {valid}");

            var txHash = UInt256.Parse("165aaffd421198fc1dd07b845537a182e173cefb526e026972fff325d532bf9a");
            var tx = await client.GetRawTransactionAsync(txHash);

        }
    }
}
