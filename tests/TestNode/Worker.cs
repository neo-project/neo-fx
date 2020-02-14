using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoFx.Models;
using NeoFx.P2P.Messages;

namespace NeoFx.TestNode
{
    class Worker : BackgroundService
    {
        private readonly IHostApplicationLifetime hostApplicationLifetime;
        private readonly ILogger<Worker> log;
        private readonly NetworkOptions networkOptions;
        private readonly NodeOptions nodeOptions;
        private readonly IRemoteNodeFactory nodeFactory;

        public Worker(IRemoteNodeFactory nodeFactory,
                      IHostApplicationLifetime hostApplicationLifetime,
                      ILogger<Worker> log,
                      IOptions<NetworkOptions> networkOptions,
                      IOptions<NodeOptions> nodeOptions)
        {
            this.hostApplicationLifetime = hostApplicationLifetime;
            this.log = log;
            this.networkOptions = networkOptions.Value;
            this.nodeOptions = nodeOptions.Value;
            this.nodeFactory = nodeFactory;
        }

        private uint Magic => networkOptions.Magic;

        private static uint GetNonce()
        {
            var random = new Random();
            Span<byte> span = stackalloc byte[4];
            random.NextBytes(span);
            return System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(span);
        }

        protected override Task ExecuteAsync(CancellationToken token)
        {
            return RunAsync(token).ContinueWith(t =>
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

        async Task RunAsync(CancellationToken token)
        {
            var (endpoint, seed) = await networkOptions.GetRandomSeedAsync();
            log.LogInformation("{seed} seed chosen", seed);

            var localVersionPayload = new VersionPayload(GetNonce(), nodeOptions.UserAgent);
            var channel = Channel.CreateUnbounded<(IRemoteNode, Message)>(new UnboundedChannelOptions()
            {
                SingleReader = true,
            });

            var remoteNode = nodeFactory.CreateRemoteNode(channel.Writer);
            await remoteNode.Connect(endpoint, localVersionPayload, token);

            await remoteNode.SendGetAddrMessage();

            await foreach (var (node, msg) in channel.Reader.ReadAllAsync(token))
            {
                switch (msg)
                {
                    case AddrMessage addrMessage:
                        log.LogInformation("Received AddrMessage {addressCount}", addrMessage.Addresses.Length);
                        foreach (var addr in addrMessage.Addresses)
                        {
                            log.LogInformation("\t{address}", addr.EndPoint);
                        }
                        break;
                    default:
                        log.LogInformation("Received {messageType}", msg.GetType().Name);
                        break;
                }
            }
        }
    }
}
