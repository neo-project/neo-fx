using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NeoFx.P2P;

namespace NeoFx.TestNode
{
    class FakeWorker : BackgroundService
    {
        private readonly ILogger<FakeWorker> _logger;

        public FakeWorker(ILogger<FakeWorker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(System.Threading.CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    class LocalNode : IHostedService
    {
        readonly ILogger<LocalNode> logger;
        readonly CancellationTokenSource cts = new CancellationTokenSource();

        public LocalNode(ILogger<LocalNode> logger)
        {
            this.logger = logger;
        }

        public void Callback(object? _)
        {
            while (!cts.IsCancellationRequested)
            {
                logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                Thread.Sleep(1000);
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            ThreadPool.QueueUserWorkItem(Callback);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            cts.Cancel();
            return Task.CompletedTask;
        }
    }

    class Program
    {
        public static Task Main(string[] args)
        {
            return CreateHostBuilder(args).Build().RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) 
        {
            if (!Directory.Exists("logs"))
            {
                Directory.CreateDirectory("logs");
            }

            var dateTimeString = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
            var logFilename = $"logs/app-{dateTimeString}.log";

            return Host.CreateDefaultBuilder(args)
                .ConfigureLogging((_, builder) => builder.AddFile(logFilename))
                // .UseWindowsService()
                // .UseSystemd()
                .ConfigureServices((context, services) =>
                {
                    services.Configure<NodeOptions>(context.Configuration.GetSection("NodeOptions"));
                    services.Configure<NetworkOptions>(context.Configuration.GetSection("NetworkOptions"));
                    services.AddTransient<IPipelineSocket, PipelineSocket>();

                    // services.AddSingleton<Storage>();
                    // services.AddSingleton<RemoteNodeManager>();
                    // services.AddSingleton<INodeConnectionFactory, NodeConnectionFactory>();
                    // services.AddSingleton<IRemoteNodeFactory, RemoteNodeFactory>();
                    services.AddHostedService<LocalNode>();
                });
        }
    }
}
