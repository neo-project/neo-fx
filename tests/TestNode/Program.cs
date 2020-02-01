using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NeoFx.P2P;

namespace NeoFx.TestNode
{
    class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    // services.AddTransient<IHeaderStorage, MemoryHeaderStorage>();
                    services.AddSingleton<IHeaderStorage>(_ => new RocksDbHeaderStorage(@"C:\Users\harry\.neofx-testnode"));
                    services.AddTransient<PipelineSocket>();
                    services.AddTransient<INodeConnection, NodeConnection>();
                    services.Configure<NodeOptions>(context.Configuration.GetSection("NodeOptions"));
                    services.Configure<NetworkOptions>(context.Configuration.GetSection("NetworkOptions"));
                    services.AddHostedService<Worker>();
                });
    }
}
