using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoFx.Models;
using NeoFx.P2P.Messages;
using System.Linq;
using System.Collections.Immutable;
using NeoFx.P2P;

namespace NeoFx.TestNode
{
    class Worker : BackgroundService
    {
        private readonly IHostApplicationLifetime hostApplicationLifetime;
        private readonly ILogger<Worker> log;
        private readonly RemoteNodeManager remoteNodeManager;

        public Worker(RemoteNodeManager remoteNodeManager,
                      IHostApplicationLifetime hostApplicationLifetime,
                      ILogger<Worker> log)
        {
            this.hostApplicationLifetime = hostApplicationLifetime;
            this.log = log;
            this.remoteNodeManager = remoteNodeManager;
        }

        protected override Task ExecuteAsync(CancellationToken token)
        {
            return remoteNodeManager.ExecuteAsync(token).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    log.LogError(t.Exception, nameof(Worker) + " exception");
                }
                else
                {
                    log.LogInformation(nameof(Worker) + " completed {IsCanceled}", t.IsCanceled);
                }
                hostApplicationLifetime.StopApplication();
            });
        }
    }
}
