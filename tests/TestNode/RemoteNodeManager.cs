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

        async Task StartReceivingMessages(IRemoteNode node, CancellationToken token)
        {
            while (true)
            {
                var message = await node.ReceiveMessage(token);
                if (message != null)
                {
                    await channel.Writer.WriteAsync((node, message), token);
                }
                else
                {
                    break;
                }
            }
        }

        async ValueTask StartProcessingMessages(CancellationToken token)
        {
            var reader = channel.Reader;
            while (true)
            {
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


        async ValueTask ProcessMessageAsync(IRemoteNode node, Message message, CancellationToken token)
        {
            log.LogInformation("Received {messageType}", message.GetType().Name);
            // switch (message)
            // {
            //     case AddrMessage addrMessage:
            //         {
            //             var addresses = new ConcurrentBag<NodeAddress>(addrMessage.Addresses);
            //             log.LogInformation("Received AddrMessage {addressesCount}", addresses.Count);

            //             var _ = Task.Run(async () => {
            //                 while (nodes.Count <= 10 && addresses.TryTake(out var address))
            //                 {
            //                     var endpoint = address.EndPoint;
                               
            //                     try
            //                     {
            //                         if (nodes.Any(n => n.RemoteEndPoint.Equals(endpoint)))
            //                         {
            //                             continue;
            //                         }

            //                         log.LogInformation("Connecting to {endpoint}", address.EndPoint);
            //                         var (node, version) = await remoteNodeFactory.ConnectAsync(endpoint, nonce, 0, channel.Writer, token);
            //                         log.LogInformation("{endpoint} connected", address.EndPoint);
            //                         nodes.Add(node);
            //                     }
            //                     catch (Exception ex)
            //                     {
            //                         log.LogWarning(ex, "{endpoint} connection failed", endpoint);
            //                     }
            //                 }
            //             });

            //         }
            //         break;
                // case HeadersMessage headersMessage:
                //     log.LogInformation("Received HeadersMessage {headersCount}", headersMessage.Headers.Length);
                //     {
                //         // var headers = headersMessage.Headers;
                //         // foo = headers.Take(10).Select(h => h.CalculateHash()).ToImmutableArray();

                //         // var hashes = headers.Skip(10).Take(10).Select(h => h.CalculateHash());
                //         // var payload = new InventoryPayload(InventoryPayload.InventoryType.Block, hashes);
                //         // await node.SendGetDataMessage(payload, token);

                //         // var (_, headerHash) = storage.GetLastHeaderHash();
                //         // // await node.SendGetHeadersMessage(new HashListPayload(headerHash));

                //         // var (blockIndex, blockHash) = storage.GetLastBlockHash();
                //         // var hashStop = storage.GetHeaderHash(blockIndex + 100);

                //         // await node.SendGetBlocksMessage(new HashListPayload(blockHash, hashStop));
                //     }
                //     break;
                // case InvMessage invMessage:
                //     log.LogInformation("Received InvMessage {type} {count}", invMessage.Type, invMessage.Hashes.Length);
                //     break;
                // // case InvMessage invMessage when invMessage.Type == InventoryPayload.InventoryType.Block:
                // //     {
                // //         // log.LogInformation("Received InvMessage {count}", invMessage.Hashes.Length);
                // //         // for (var x = 0; x < invMessage.Hashes; x++)
                // //         // {
                // //         //     storage.AddBlockHash()
                // //         // }
                // //         // await node.SendGetDataMessage(invMessage.Payload, token);
                // //     }
                // //     break;
                // case BlockMessage blocKMessage:
                //     {
                //         log.LogInformation("Received BlockMessage {index}", blocKMessage.Block.Index);
                //         storage.AddBlock(blocKMessage.Block);
                //     }
                //     break;
            //     default:
            //         log.LogInformation("Received {messageType}", message.GetType().Name);
            //         break;
            // }
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
                catch(Exception ex)
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
