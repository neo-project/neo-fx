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
        private readonly Storage storage;

        public Worker(IRemoteNodeFactory nodeFactory,
                      Storage storage,
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

        ImmutableArray<UInt256> foo = ImmutableArray<UInt256>.Empty;

        async Task ProcessMessage(IRemoteNode node, Message message, CancellationToken token)
        {
            switch (message)
            {
                // case AddrMessage addrMessage:
                //     log.LogInformation("Received AddrMessage {addressCount}", addrMessage.Addresses.Length);
                //     foreach (var addr in addrMessage.Addresses)
                //     {
                //         log.LogInformation("\t{address}", addr.EndPoint);
                //     }
                //     break;
                case HeadersMessage headersMessage:
                    log.LogInformation("Received HeadersMessage {headersCount}", headersMessage.Headers.Length);
                    {
                        var headers = headersMessage.Headers;
                        foo = headers.Take(10).Select(h => h.CalculateHash()).ToImmutableArray();

                        var hashes = headers.Skip(10).Take(10).Select(h => h.CalculateHash());
                        var payload = new InventoryPayload(InventoryPayload.InventoryType.Block, hashes);
                        await node.SendGetDataMessage(payload, token);

                        // var (_, headerHash) = storage.GetLastHeaderHash();
                        // // await node.SendGetHeadersMessage(new HashListPayload(headerHash));

                        // var (blockIndex, blockHash) = storage.GetLastBlockHash();
                        // var hashStop = storage.GetHeaderHash(blockIndex + 100);

                        // await node.SendGetBlocksMessage(new HashListPayload(blockHash, hashStop));
                    }
                    break;
                case InvMessage invMessage:
                    log.LogInformation("Received InvMessage {type} {count}", invMessage.Type, invMessage.Hashes.Length);
                    break;
                // case InvMessage invMessage when invMessage.Type == InventoryPayload.InventoryType.Block:
                //     {
                //         // log.LogInformation("Received InvMessage {count}", invMessage.Hashes.Length);
                //         // for (var x = 0; x < invMessage.Hashes; x++)
                //         // {
                //         //     storage.AddBlockHash()
                //         // }
                //         // await node.SendGetDataMessage(invMessage.Payload, token);
                //     }
                //     break;
                case BlockMessage blocKMessage:
                    {
                        log.LogInformation("Received BlockMessage {index}", blocKMessage.Block.Index);
                        storage.AddBlock(blocKMessage.Block);

                        if (blocKMessage.Block.Index == 18)
                        {
                            var payload = new InventoryPayload(InventoryPayload.InventoryType.Block, foo);
                            await node.SendGetDataMessage(payload, token);
                        }
                    }
                    break;
                default:
                    log.LogInformation("Received {messageType}", message.GetType().Name);
                    break;
            }
        }
        
        async Task RunAsync(CancellationToken token)
        {
            var (endpoint, seed) = await networkOptions.GetRandomSeedAsync();
            log.LogInformation("{seed} seed chosen", seed);

            var localVersionPayload = new VersionPayload(GetNonce(), nodeOptions.UserAgent);
            var channel = Channel.CreateUnbounded<(IRemoteNode node, Message message)>(new UnboundedChannelOptions()
            {
                SingleReader = true,
            });

            var remoteNode = nodeFactory.CreateRemoteNode(channel.Writer);
            await remoteNode.Connect(endpoint, localVersionPayload, token);

            var (index, hash) = storage.GetLastBlockHash();
            log.LogInformation("initial block header height {index}", index);

            await remoteNode.SendGetHeadersMessage(new HashListPayload(hash));

            var completionTask = channel.Reader.Completion; 
            while (!completionTask.IsCompleted)
            {
                while (channel.Reader.TryRead(out var item))
                {
                    await ProcessMessage(item.node, item.message, token);
                }

                storage.Cleanup();

                await channel.Reader.WaitToReadAsync(token);
            }
        }
    }
}
