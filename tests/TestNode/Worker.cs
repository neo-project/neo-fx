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
using System.Linq;
using System.Collections.Immutable;

namespace NeoFx.TestNode
{
    class Worker : BackgroundService
    {
        private readonly IHostApplicationLifetime hostApplicationLifetime;
        private readonly ILogger<Worker> log;
        private readonly NetworkOptions networkOptions;
        private readonly NodeOptions nodeOptions;
        private readonly IRemoteNodeFactory nodeFactory;
        private readonly IStorage storage;

        public Worker(IRemoteNodeFactory nodeFactory,
                      IStorage storage,
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
            this.storage = storage;
        }

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

            var (index, hash) = storage.GetLastBlockHash();
            log.LogInformation("initial block height {index}", index);
            if (index < remoteNode.VersionPayload.StartHeight)
            {
                await remoteNode.SendGetBlocksMessage(new HashListPayload(hash));
            }

            await remoteNode.SendGetAddrMessage(token);

            await foreach (var (node, msg) in channel.Reader.ReadAllAsync(token))
            {
                switch (msg)
                {
                    // case AddrMessage addrMessage:
                    //     log.LogInformation("Received AddrMessage {addressCount}", addrMessage.Addresses.Length);
                    //     foreach (var addr in addrMessage.Addresses)
                    //     {
                    //         log.LogInformation("\t{address}", addr.EndPoint);
                    //     }
                    //     break;
                    // case HeadersMessage headersMessage:
                    //     log.LogInformation("Received HeadersMessage {headersCount}", headersMessage.Headers.Length);
                    //     break;
                    case InvMessage invMessage when invMessage.Type == InventoryPayload.InventoryType.Block:
                        {
                            log.LogInformation("Received InvMessage {count}", invMessage.Hashes.Length);
                            await node.SendGetDataMessage(invMessage.Payload);
                        }
                        break;
                    case BlockMessage blocKMessage:
                        {
                            log.LogInformation("Received BlockMessage {index}", blocKMessage.Block.Index);
                            storage.AddBlock(blocKMessage.Block);
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
