using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoreLinq;
using NeoFx.Models;
using NeoFx.P2P.Messages;

namespace NeoFx.TestNode
{
    class RemoteNodeManager
    {
        private readonly IBlockchain blockchain;
        private readonly IRemoteNodeFactory remoteNodeFactory;
        private readonly IHostApplicationLifetime hostApplicationLifetime;
        private readonly NetworkOptions networkOptions;
        private readonly ILogger<RemoteNodeManager> log;
        private readonly uint nonce;
        private readonly Channel<(IRemoteNode node, Message message)> channel = Channel.CreateUnbounded<(IRemoteNode node, Message msg)>();
        private ImmutableList<IRemoteNode> connectedNodes = ImmutableList<IRemoteNode>.Empty;
        private ImmutableHashSet<IPEndPoint> unconnectedNodes = ImmutableHashSet<IPEndPoint>.Empty;
        readonly TaskGuard connectPeers;
        readonly TaskGuard checkBlockGap;

        public RemoteNodeManager(
            IBlockchain blockchain,
            IRemoteNodeFactory remoteNodeFactory,
            IHostApplicationLifetime hostApplicationLifetime,
            IOptions<NetworkOptions> networkOptions,
            ILogger<RemoteNodeManager> logger)
        {
            this.blockchain = blockchain;
            this.remoteNodeFactory = remoteNodeFactory;
            this.hostApplicationLifetime = hostApplicationLifetime;
            this.networkOptions = networkOptions.Value;
            this.log = logger;

            connectPeers = new TaskGuard(PeerConnectorAsync, nameof(PeerConnectorAsync), logger, 10);
            checkBlockGap = new TaskGuard(GapCheckAsync, nameof(GapCheckAsync), logger, 10);

            var random = new Random();
            Span<byte> span = stackalloc byte[4];
            random.NextBytes(span);
            nonce = BinaryPrimitives.ReadUInt32LittleEndian(span);
        }

        public void Execute(CancellationToken token)
        {
            ExecuteAsync(token)
                .LogResult(log, nameof(ExecuteAsync), ex => channel.Writer.Complete(ex));
        }

        async Task ExecuteAsync(CancellationToken token)
        {
            var node = await ConnectSeed(token);
            StartReceivingMessages(node, token)
                .LogResult(log, nameof(StartReceivingMessages));

            await StartProcessingMessages(token);
        }

        void AddUnconnectedNodes(IEnumerable<IPEndPoint> endpoints)
        {
            ImmutableInterlocked.Update(ref unconnectedNodes, original =>
            {
                foreach (var endpoint in endpoints)
                {
                    if (connectedNodes.Any(n => n.RemoteEndPoint.Equals(endpoint)))
                        continue;
                    original = original.Add(endpoint);
                }
                return original;
            });
        }

        void RemoveUnconnectedNode(IPEndPoint endpoint)
        {
            ImmutableInterlocked.Update(ref unconnectedNodes, original => original.Remove(endpoint));
        }

        async Task StartReceivingMessages(IRemoteNode node, CancellationToken token)
        {
            log.LogInformation("StartReceivingMessages {node}", node.RemoteEndPoint);

            ImmutableInterlocked.Update(ref connectedNodes, original => original.Add(node));
            while (true)
            {
                var message = await node.ReceiveMessage(token);
                if (message != null)
                {
                    log.LogDebug("Received message on {address}", node.RemoteEndPoint);
                    await channel.Writer.WriteAsync((node, message), token);
                }
                else
                {
                    log.LogWarning("{address} disconnected", node.RemoteEndPoint);
                    ImmutableInterlocked.Update(ref connectedNodes, original => original.Remove(node));
                    break;
                }
            }
        }

        async Task StartProcessingMessages(CancellationToken token)
        {
            var reader = channel.Reader;
            while (true)
            {
                connectPeers.Run(token);

                while (reader.TryRead(out var item))
                {
                    await ProcessMessageAsync(item.node, item.message, token);
                }

                if (!await reader.WaitToReadAsync(token))
                {
                    return;
                }
            }
        }

        async Task GapCheckAsync(CancellationToken token)
        {
            var gap = await blockchain.TryGetBlockGap();
            log.LogInformation(nameof(GapCheckAsync) + " {success} {start} {stop}", gap.success, gap.start, gap.stop);

            if (gap.success)
            {
                log.LogInformation("Sending GetBlocks {start} {stop}", gap.start, gap.stop);
                var payload = new HashListPayload(gap.start);

                var nodes = connectedNodes;
                foreach (var node in nodes)
                {
                    await node.SendGetBlocksMessage(payload, token);
                }
            } 
        }

        async Task PeerConnectorAsync(CancellationToken token)
        {
            while (connectedNodes.Count <= 10 && !token.IsCancellationRequested)
            {
                log.LogInformation(nameof(PeerConnectorAsync) + " Connected: {connected} / Unconnected: {unconnected}",
                    connectedNodes.Count, unconnectedNodes.Count);

                var endpoint = unconnectedNodes.FirstOrDefault();
                if (endpoint == null)
                    break;

                RemoveUnconnectedNode(endpoint);

                try
                {
                    log.LogInformation("Connecting to {endpoint}", endpoint);
                    var (node, version) = await remoteNodeFactory.ConnectAsync(endpoint, nonce, 0, token);
                    log.LogInformation("{endpoint} connected", endpoint);
                    await node.SendGetAddrMessage(token);
                    StartReceivingMessages(node, token)
                        .LogResult(log, nameof(StartReceivingMessages));
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "{endpoint} connection failed", endpoint);
                }
            }
        }

        async Task ProcessMessageAsync(IRemoteNode node, Message message, CancellationToken token)
        {
            switch (message)
            {
                case AddrMessage addrMessage:
                    {
                        var addresses = addrMessage.Addresses;
                        log.LogInformation("Received AddrMessage {addressesCount} {node}", addresses.Length, node.RemoteEndPoint);
                        AddUnconnectedNodes(addresses.Select(a => a.EndPoint));
                    }
                    break;
                case HeadersMessage headersMessage:
                    {
                        var headers = headersMessage.Headers;
                        log.LogInformation("Received HeadersMessage {headersCount} {node}", headers.Length, node.RemoteEndPoint);

                        // The Neo docs suggest sending a getblocks message to retrieve a list 
                        // of block hashes to sync. However, we can calculate the block hashes 
                        // from the headers in this message without needing the extra round trip

                        var (index, _) = await blockchain.GetLastBlockHash();
                        foreach (var batch in headers.Where(h => h.Index > index).Batch(500))
                        {
                            var payload = new InventoryPayload(InventoryPayload.InventoryType.Block, batch.Select(h => h.CalculateHash()));
                            await node.SendGetDataMessage(payload, token);
                        }
                    }
                    break;
                case InvMessage invMessage when invMessage.Type == InventoryPayload.InventoryType.Block:
                    {
                        var hashes = invMessage.Hashes;
                        log.LogInformation("Received Block InvMessage {count} {node}", hashes.Length, node.RemoteEndPoint);
                        await node.SendGetDataMessage(invMessage.Payload, token);
                        checkBlockGap.Run(token);
                    }
                    break;
                case BlockMessage blockMessage:
                    {
                        log.LogInformation("Received BlockMessage {index} {node}", blockMessage.Block.Index, node.RemoteEndPoint);
                        await blockchain.AddBlock(blockMessage.Block);
                    }
                    break;
                default:
                    log.LogInformation("Received {messageType} {node}", message.GetType().Name, node.RemoteEndPoint);
                    break;
            }
        }

        async ValueTask<IRemoteNode> ConnectSeed(CancellationToken token)
        {
            var (index, hash) = await blockchain.GetLastBlockHash();

            await foreach (var (endpoint, seed) in ResolveSeeds(networkOptions.Seeds))
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    log.LogInformation("Connecting to {seed}", seed);
                    var (node, version) = await remoteNodeFactory.ConnectAsync(endpoint, nonce, index, token);
                    log.LogInformation("{seed} connected", seed);

                    await node.SendGetAddrMessage(token);
                    if (version.StartHeight > index)
                    {
                        await node.SendGetHeadersMessage(new HashListPayload(hash), token);
                    }

                    return node;
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "{seed} connection failed", seed);
                }
            }

            var errorMessage = "could not connect to any seed nodes";
            log.LogCritical(errorMessage);
            throw new System.IO.IOException(errorMessage);

            static async IAsyncEnumerable<(IPEndPoint, string)> ResolveSeeds(string[] seeds)
            {
                foreach (var seed in seeds)
                {
                    var colonIndex = seed.IndexOf(':');
                    var host = seed.Substring(0, colonIndex);
                    var port = int.Parse(seed.AsSpan().Slice(colonIndex + 1));
                    var addresses = await Dns.GetHostAddressesAsync(host);
                    if (addresses.Length > 0)
                    {
                        var endPoint = new IPEndPoint(addresses[0], port);
                        yield return (endPoint, seed);
                    }
                }
            }
        }
    }
}
