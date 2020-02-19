using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NeoFx.TestNode
{
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
}
