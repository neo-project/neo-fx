using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoFx.P2P.Messages;
using System.Buffers.Binary;

namespace NeoFx.TestNode
{
    class RemoteNodeManager : IDisposable
    {
        private readonly IRemoteNodeFactory remoteNodeFactory;
        private readonly Storage storage;
        private readonly NetworkOptions networkOptions;
        private readonly ILogger<RemoteNodeManager> log;
        private readonly uint nonce;
        private readonly string userAgent;
        private readonly List<(IRemoteNode node, ChannelReader<Message> reader)> nodes = new List<(IRemoteNode, ChannelReader<Message>)>(10);

        public RemoteNodeManager(
            IRemoteNodeFactory remoteNodeFactory,
            Storage storage,
            IOptions<NetworkOptions> networkOptions, 
            IOptions<NodeOptions> nodeOptions,
            ILogger<RemoteNodeManager> logger)
        {
            this.remoteNodeFactory = remoteNodeFactory;
            this.storage = storage;
            this.networkOptions = networkOptions.Value;
            this.log = logger;
            this.userAgent = nodeOptions.Value.UserAgent;

            var random = new Random();
            Span<byte> span = stackalloc byte[4];
            random.NextBytes(span);
            nonce = BinaryPrimitives.ReadUInt32LittleEndian(span);
        }

        public void Dispose()
        {
            storage.Dispose();
        }

        async ValueTask ConnectSeed(CancellationToken token)
        {
            var (index, hash) = storage.GetLastBlockHash();
            var localVersion = new VersionPayload(nonce, userAgent, index);

            var seedChannel = Channel.CreateUnbounded<Message>(new UnboundedChannelOptions()
            {
                SingleReader = true,
            });

            await foreach (var (endpoint, seed) in networkOptions.GetSeeds())
            {
                log.LogInformation("Connecting to {seed}", seed);

                try
                {
                    var seedNode = remoteNodeFactory.CreateRemoteNode(seedChannel.Writer);
                    var seedVersion = await seedNode.Connect(endpoint, new VersionPayload(nonce, userAgent, index), token);
                    log.LogInformation("{seed} connected", seed);

                    await seedNode.SendGetAddrMessage();
                    if (seedVersion.StartHeight > index)
                    {
                        await seedNode.SendGetHeadersMessage(new HashListPayload(hash));
                    }
                    nodes.Add((seedNode, seedChannel.Reader));
                    return;
                }
                catch(Exception ex)
                {
                    log.LogError(ex, "{seed} connection failed", seed);
                }
            }

            throw new Exception("could not connect to any seed nodes");
        }

        public void Execute(CancellationToken token)
        {
            ExecuteAsync(token)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        log.LogError(t.Exception, nameof(RemoteNodeManager) + " exception");
                    }
                    else
                    {
                        log.LogInformation(nameof(RemoteNodeManager) + " completed {IsCanceled}", t.IsCanceled);
                    }
                    // Cleanup?
                });
        }

        async Task ExecuteAsync(CancellationToken token)
        {
            await ConnectSeed(token);

            while (true)
            {
                for (var x = 0; x < nodes.Count; x++)
                {
                    var (node, reader) = nodes[x];
                    while (reader.TryRead(out var message))
                    {
                        await ProcessMessage(node, message, token);
                    }
                }

                for (int i = nodes.Count - 1; i >= 0; i--)
                {
                    if (nodes[i].reader.Completion.IsCompleted)
                    {
                        nodes.RemoveAt(i);
                    }
                }
            }
        }

        async ValueTask ProcessAddrMessage(AddrMessage addrMessage, CancellationToken token)
        {
            var queue = new Queue<NodeAddress>(addrMessage.Addresses);
            log.LogInformation("Received AddrMessage {addressCount}", queue.Count);

            var localVersion = new VersionPayload(nonce, userAgent);
            
            while (nodes.Count < nodes.Capacity && queue.Count > 0)
            {
                var channel = Channel.CreateUnbounded<Message>(new UnboundedChannelOptions()
                {
                    SingleReader = true,
                });

                while (queue.TryDequeue(out var address))
                {
                    var endpoint = address.EndPoint;
                    log.LogInformation("Connecting to {address}:{port}", endpoint.Address, endpoint.Port);

                    try
                    {
                        var node = remoteNodeFactory.CreateRemoteNode(channel.Writer);
                        await node.Connect(endpoint, new VersionPayload(nonce, userAgent), token);
                        log.LogInformation("{address}:{port} connected", endpoint.Address, endpoint.Port);

                        lock(nodes)
                        {
                            nodes.Add((node, channel.Reader));
                        }

                        break;
                    }
                    catch (Exception _)
                    {
                        log.LogWarning("{address}:{port} connection failed", endpoint.Address, endpoint.Port);
                    }
                }
            }
        }

        async Task ProcessMessage(IRemoteNode node, Message message, CancellationToken token)
        {
            switch (message)
            {
                case AddrMessage addrMessage:
                    // await ProcessAddrMessage(addrMessage, token);
                    break;
                case HeadersMessage headersMessage:
                    log.LogInformation("Received HeadersMessage {headersCount}", headersMessage.Headers.Length);
                    {
                        // var headers = headersMessage.Headers;
                        // foo = headers.Take(10).Select(h => h.CalculateHash()).ToImmutableArray();

                        // var hashes = headers.Skip(10).Take(10).Select(h => h.CalculateHash());
                        // var payload = new InventoryPayload(InventoryPayload.InventoryType.Block, hashes);
                        // await node.SendGetDataMessage(payload, token);

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
                    }
                    break;
                default:
                    log.LogInformation("Received {messageType}", message.GetType().Name);
                    break;
            }
        }
    }
}
