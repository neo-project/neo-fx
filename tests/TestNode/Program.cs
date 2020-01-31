using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NeoFx.TestNode.Options;

namespace NeoFx.TestNode
{
    static class foo
    {

    }
    class Program
    {
        private static readonly Dictionary<string, string> nodeConfig =
            new Dictionary<string, string>
            {
                { "NodeOptions:UserAgent", "/NeoFx.TestNode:0.1.0/" }
            };

        private static readonly Dictionary<string, string> testNetConfig =
            new Dictionary<string, string>
            {
                {"NetworkOptions:Magic", "1953787457"},
                {"NetworkOptions:Seeds:0", "seed1.ngd.network:20333"},
                {"NetworkOptions:Seeds:1", "seed2.ngd.network:20333"},
                {"NetworkOptions:Seeds:2", "seed3.ngd.network:20333"},
                {"NetworkOptions:Seeds:3", "seed4.ngd.network:20333"},
                {"NetworkOptions:Seeds:4", "seed5.ngd.network:20333"},
                {"NetworkOptions:Seeds:5", "seed6.ngd.network:20333"},
                {"NetworkOptions:Seeds:6", "seed7.ngd.network:20333"},
                {"NetworkOptions:Seeds:7", "seed8.ngd.network:20333"},
                {"NetworkOptions:Seeds:8", "seed9.ngd.network:20333"},
                {"NetworkOptions:Seeds:9", "seed10.ngd.network:20333"},
                {"NetworkOptions:Validators:0", "0327da12b5c40200e9f65569476bbff2218da4f32548ff43b6387ec1416a231ee8"},
                {"NetworkOptions:Validators:1", "026ce35b29147ad09e4afe4ec4a7319095f08198fa8babbe3c56e970b143528d22"},
                {"NetworkOptions:Validators:2", "0209e7fd41dfb5c2f8dc72eb30358ac100ea8c72da18847befe06eade68cebfcb9"},
                {"NetworkOptions:Validators:3", "039dafd8571a641058ccc832c5e2111ea39b09c0bde36050914384f7a48bce9bf9"},
                {"NetworkOptions:Validators:4", "038dddc06ce687677a53d54f096d2591ba2302068cf123c1f2d75c2dddc5425579"},
                {"NetworkOptions:Validators:5", "02d02b1873a0863cd042cc717da31cea0d7cf9db32b74d4c72c01b0011503e2e22"},
                {"NetworkOptions:Validators:6", "034ff5ceeac41acf22cd5ed2da17a6df4dd8358fcb2bfb1a43208ad0feaab2746b"},
            };

        private static async Task Main()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureLogging(logging => 
                {
                    // logging.AddFilter("NeoFx.P2P.NeoClient", LogLevel.Trace);
                    // logging.AddFilter("NeoFx.P2P.PipelineSocket", LogLevel.Trace);
                })
                .ConfigureAppConfiguration((_, builder) =>
                {
                    builder.AddInMemoryCollection(testNetConfig);
                    builder.AddInMemoryCollection(nodeConfig);
                })
                .ConfigureServices((context, services) =>
                {
                    services.Configure<NodeOptions>(context.Configuration.GetSection("NodeOptions"));
                    services.Configure<NetworkOptions>(context.Configuration.GetSection("NetworkOptions"));
                    services.AddHostedService<Worker>();
                })
                .Build();

            await host.RunAsync().ConfigureAwait(false);
        }
    }
}
