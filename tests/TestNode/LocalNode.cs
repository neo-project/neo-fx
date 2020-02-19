using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NeoFx.TestNode
{
    class LocalNode : IHostedService
    {
        readonly RemoteNodeManager remoteNodeManager;
        readonly ILogger<LocalNode> logger;
        readonly CancellationTokenSource cts = new CancellationTokenSource();

        public LocalNode(RemoteNodeManager remoteNodeManager, ILogger<LocalNode> logger)
        {
            this.remoteNodeManager = remoteNodeManager;
            this.logger = logger;
        }

        public Task StartAsync(CancellationToken _)
        {
            remoteNodeManager.Execute(cts.Token);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken _)
        {
            cts.Cancel();
            return Task.CompletedTask;
        }
    }
}
