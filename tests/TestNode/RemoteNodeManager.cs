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
    interface IRemoteNodeManager
    {
        Task ConnectAsync(ChannelWriter<(IRemoteNode node, Message message)> writer, uint index, UInt256 hash, CancellationToken token);
        void AddAddresses(ImmutableArray<NodeAddress> nodeAddresses);
    }

    class RemoteNodeManager : IRemoteNodeManager
    {
        private readonly IHostApplicationLifetime hostApplicationLifetime;
        private readonly IRemoteNodeFactory remoteNodeFactory;
        private readonly ILogger<RemoteNodeManager> log;
        private readonly uint nonce;
        private readonly ImmutableArray<string> seeds;

        private ChannelWriter<(IRemoteNode node, Message message)>? writer;
        private Timer? connectPeersTimer;

        private ImmutableList<IRemoteNode> connectedNodes = ImmutableList<IRemoteNode>.Empty;
        private ImmutableHashSet<IPEndPoint> unconnectedNodes = ImmutableHashSet<IPEndPoint>.Empty;
        // readonly TaskGuard connectPeers;
        // readonly TaskGuard checkBlockGap;

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



            // connectPeers = new TaskGuard(PeerConnectorAsync, nameof(PeerConnectorAsync), logger, 10);
            // checkBlockGap = new TaskGuard(GapCheckAsync, nameof(GapCheckAsync), logger, 10);
        }

        public async Task ConnectAsync(ChannelWriter<(IRemoteNode node, Message message)> writer, uint index, UInt256 hash, CancellationToken token)
        {
            if (Interlocked.CompareExchange(ref this.writer, writer, null) == null)
            {
                var seedNode = await ConnectSeedAsync(index, hash, token);
                StartReceivingMessages(seedNode, writer, token)
                    .LogResult(log, nameof(StartReceivingMessages));

                connectPeersTimer = new Timer(_ => {
                    AddConnectionsAsync().LogResult(log, nameof(AddConnectionsAsync));
                }, null, default, TimeSpan.FromSeconds(10));
            }
            else
            {
                log.LogError("Already connected");
            }
        }

        public void AddAddresses(ImmutableArray<NodeAddress> nodeAddresses)
        {
            log.LogInformation("Adding {count} addresses", nodeAddresses.Length);

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


        // async Task GapCheckAsync(CancellationToken token)
        // {
        //     var gap = await blockchain.TryGetBlockGap();
        //     log.LogInformation(nameof(GapCheckAsync) + " {success} {start} {stop}", gap.success, gap.start, gap.stop);

        //     if (gap.success)
        //     {
        //         log.LogInformation("Sending GetBlocks {start} {stop}", gap.start, gap.stop);
        //         var payload = new HashListPayload(gap.start);

        //         var nodes = connectedNodes;
        //         foreach (var node in nodes)
        //         {
        //             await node.SendGetBlocksMessage(payload, token);
        //         }
        //     } 
        // }

        int addConnectionsRunning = 0;

        async Task AddConnectionsAsync()
        {
            if (writer != null 
                && unconnectedNodes.Count > 0
                && Interlocked.CompareExchange(ref addConnectionsRunning, 1, 0) == 1)
            {
                try
                {
                    var token = hostApplicationLifetime.ApplicationStopping;
                    while (connectedNodes.Count <= 10 && !token.IsCancellationRequested)
                    {
                        log.LogInformation(nameof(AddConnectionsAsync) + " Connected: {connected} / Unconnected: {unconnected}",
                            connectedNodes.Count, unconnectedNodes.Count);

                        var endpoint = unconnectedNodes.FirstOrDefault();
                        if (endpoint == null)
                            break;

                        ImmutableInterlocked.Update(ref unconnectedNodes, original => original.Remove(endpoint));

                        try
                        {
                            log.LogInformation("Connecting to {endpoint}", endpoint);
                            var (node, version) = await remoteNodeFactory.ConnectAsync(endpoint, nonce, 0, token);
                            log.LogInformation("{endpoint} connected", endpoint);
                            await node.SendGetAddrMessage(token);
                            StartReceivingMessages(node, writer, token)
                                .LogResult(log, nameof(StartReceivingMessages));
                        }
                        catch (Exception ex)
                        {
                            log.LogWarning(ex, "{endpoint} connection failed", endpoint);
                        }
                    }
                }
                finally
                {
                    addConnectionsRunning = 0;
                }
            }
        }

        async Task<IRemoteNode> ConnectSeedAsync(uint index, UInt256 hash, CancellationToken token)
        {
            await foreach (var (endpoint, seed) in ResolveSeeds(seeds))
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
}
