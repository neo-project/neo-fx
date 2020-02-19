using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoFx.P2P.Messages;

namespace NeoFx.TestNode
{
    class RemoteNodeManager
    {
        private readonly IBlockchain blockchain;
        private readonly IRemoteNodeFactory remoteNodeFactory;
        private readonly NetworkOptions networkOptions;
        private readonly ILogger<RemoteNodeManager> log;
        private readonly uint nonce;
        private readonly Channel<Message> channel = Channel.CreateUnbounded<Message>();
        
        public RemoteNodeManager(
            IBlockchain blockchain,
            IRemoteNodeFactory remoteNodeFactory,
            IOptions<NetworkOptions> networkOptions, 
            ILogger<RemoteNodeManager> logger)
        {
            this.blockchain = blockchain;
            this.remoteNodeFactory = remoteNodeFactory;
            this.networkOptions = networkOptions.Value;
            this.log = logger;

            var random = new Random();
            Span<byte> span = stackalloc byte[4];
            random.NextBytes(span);
            nonce = BinaryPrimitives.ReadUInt32LittleEndian(span);
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
                    channel.Writer.Complete(t.Exception);
                });

        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            var (index, hash) = await blockchain.GetLastBlockHash();
            var seedNode = await ConnectSeed(index, hash, token);

            var reader = channel.Reader;
            while (!reader.Completion.IsCompleted)
            {
                while (reader.TryRead(out var message))
                {
                    log.LogInformation("Received {messageType}", message.GetType().Name);
                }

                if (!await reader.WaitToReadAsync())
                {
                    return;
                }
            }
        }

        async ValueTask<IRemoteNode> ConnectSeed(uint height, UInt256 hash, CancellationToken token)
        {
            await foreach (var (endpoint, seed) in ResolveSeeds(networkOptions.Seeds))
            {
                try
                {
                    log.LogInformation("Connecting to {seed}", seed);
                    var (node, version) = await remoteNodeFactory.ConnectAsync(endpoint, nonce, height, channel.Writer, token);
                    log.LogInformation("{seed} connected", seed);

                    await node.SendGetAddrMessage();
                    if (version.StartHeight > height)
                    {
                        await node.SendGetHeadersMessage(new HashListPayload(hash));
                    }

                    return node;
                }
                catch(Exception ex)
                {
                    log.LogError(ex, "{seed} connection failed", seed);
                }
            }

            throw new Exception("could not connect to any seed nodes");

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
