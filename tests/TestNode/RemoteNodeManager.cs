using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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
    interface IRemoteNodeManager
    {
        Task ConnectAsync(ChannelWriter<(IRemoteNode node, Message message)> writer, uint index, UInt256 hash, CancellationToken token);
        void AddAddresses(ImmutableArray<NodeAddress> nodeAddresses);
        Task BroadcastGetBlocks(UInt256 start, UInt256 stop, CancellationToken token);
    }

    class RemoteNodeManager : IRemoteNodeManager
    {
        private readonly IHostApplicationLifetime hostApplicationLifetime;
        private readonly IRemoteNodeFactory remoteNodeFactory;
        private readonly ILogger<RemoteNodeManager> log;
        private readonly uint nonce;
        private readonly ImmutableArray<string> seeds;

        private ChannelWriter<(IRemoteNode node, Message message)>? writer;
        private TaskGuard connectPeersTask;
        private Timer? connectPeersTimer;

        private ImmutableList<IRemoteNode> connectedNodes = ImmutableList<IRemoteNode>.Empty;
        private ImmutableHashSet<IPEndPoint> unconnectedNodes = ImmutableHashSet<IPEndPoint>.Empty;

        public RemoteNodeManager(
            IHostApplicationLifetime hostApplicationLifetime,
            ILogger<RemoteNodeManager> logger,
            IOptions<NetworkOptions> networkOptions,
            IOptions<NodeOptions> nodeOptions,
            IRemoteNodeFactory remoteNodeFactory)
        {
            this.hostApplicationLifetime = hostApplicationLifetime;
            this.log = logger;
            this.seeds = networkOptions.Value.Seeds.ToImmutableArray();
            this.nonce = nodeOptions.Value.Nonce;
            this.remoteNodeFactory = remoteNodeFactory;

            connectPeersTask = new TaskGuard(RunAddConnections, nameof(RunAddConnections), log);
        }

        public async Task ConnectAsync(ChannelWriter<(IRemoteNode node, Message message)> writer, uint index, UInt256 hash, CancellationToken token)
        {
            if (Interlocked.CompareExchange(ref this.writer, writer, null) == null)
            {
                unconnectedNodes = await InitializeEndpoints();
                await ConnectSeedAsync(index, hash, token);

                connectPeersTimer = new Timer(_ => 
                {
                    if (connectedNodes.Count < 10 && unconnectedNodes.Count > 0)
                    {
                        connectPeersTask.Run(hostApplicationLifetime.ApplicationStopping);
                    }
                }, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
            }
            else
            {
                log.LogError("Already connected");
            }
        }

        public void AddAddresses(ImmutableArray<NodeAddress> nodeAddresses)
        {
            ImmutableInterlocked.Update(ref unconnectedNodes, original =>
            {
                foreach (var address in nodeAddresses)
                {
                    var endpoint = address.EndPoint;
                    if (connectedNodes.Any(n => n.RemoteEndPoint.Equals(endpoint)))
                        continue;
                    original = original.Add(endpoint);
                }
                return original;
            });

            log.LogInformation("Received {count} addresses {total}", nodeAddresses.Length, unconnectedNodes.Count);
        }

        public async Task<ImmutableHashSet<IPEndPoint>> InitializeEndpoints()
        {
            var builder = ImmutableHashSet.CreateBuilder<IPEndPoint>();
            await foreach (var (endpoint, seed) in ResolveSeeds(seeds))
            {
                builder.Add(endpoint);
            }
            return builder.ToImmutable();
        }

        async Task StartReceivingMessages(IRemoteNode node, ChannelWriter<(IRemoteNode node, Message message)> writer, CancellationToken token)
        {
            log.LogInformation("StartReceivingMessages {node}", node.RemoteEndPoint);

            ImmutableInterlocked.Update(ref connectedNodes, original => original.Add(node));
            while (true)
            {
                var message = await node.ReceiveMessage(token);
                if (message != null)
                {
                    log.LogDebug("Received message on {address}", node.RemoteEndPoint);
                    await writer.WriteAsync((node, message), token);
                }
                else
                {
                    log.LogWarning("{address} disconnected", node.RemoteEndPoint);
                    ImmutableInterlocked.Update(ref connectedNodes, original => original.Remove(node));
                    break;
                }
            }
        }

        IEnumerable<IRemoteNode> GetRandomRemoteNodes()
        {
            var r = new Random();
            var nodes = connectedNodes;
            return nodes
                .OrderBy(n => r.NextDouble())
                .Take(Math.Min(nodes.Count / 2, 3));
        }

        public async Task BroadcastGetBlocks(UInt256 start, UInt256 stop, CancellationToken token)
        {
            var payload = new HashListPayload(start, stop);
            foreach (var node in GetRandomRemoteNodes())
            {
                log.LogInformation("BroadcastGetBlocks {start} {stop} {node}", start, stop, node.RemoteEndPoint);
                await node.SendGetBlocksMessage(payload, token);
            }
        }

        async Task<IRemoteNode?> ConnectNodeAsync(uint startHeight, CancellationToken token)
        {
            var endpoint = unconnectedNodes.FirstOrDefault();
            if (endpoint == null || writer == null)
                return null;

            ImmutableInterlocked.Update(ref unconnectedNodes, original => original.Remove(endpoint));

            try
            {
                log.LogInformation("Connecting to {endpoint}", endpoint);
                var (node, version) = await remoteNodeFactory.ConnectAsync(endpoint, nonce, startHeight, token);
                log.LogInformation("{endpoint} connected", endpoint);
                await node.SendGetAddrMessage(token);
                StartReceivingMessages(node, writer, token)
                    .LogResult(log, nameof(StartReceivingMessages));
                return node;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "{endpoint} connection failed", endpoint);
                return null;
            }
        }

        async Task RunAddConnections(CancellationToken token)
        {
            while (unconnectedNodes.Count > 0
                && connectedNodes.Count <= 20 
                && !token.IsCancellationRequested)
            {
                log.LogInformation(nameof(RunAddConnections) + " Connected: {connected} / Unconnected: {unconnected}",
                    connectedNodes.Count, unconnectedNodes.Count);

                var node = await ConnectNodeAsync(0, token);
            }
        }

        async Task ConnectSeedAsync(uint index, UInt256 hash, CancellationToken token)
        {
            while (unconnectedNodes.Count > 0)
            {
                token.ThrowIfCancellationRequested();

                var node = await ConnectNodeAsync(index, token);
                if (node != null)
                {
                    await node.SendGetHeadersMessage(new HashListPayload(hash), token);
                    return;
                }
            }

            var errorMessage = "could not connect to any seed nodes";
            log.LogCritical(errorMessage);
            throw new System.IO.IOException(errorMessage);
        }

        static async IAsyncEnumerable<(IPEndPoint, string)> ResolveSeeds(ImmutableArray<string> seeds)
        {
            for (int i = 0; i < seeds.Length; i++)
            {
                string seed = seeds[i];
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
