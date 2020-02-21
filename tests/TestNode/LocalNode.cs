using System;
using System.Buffers.Binary;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MoreLinq;
using NeoFx.P2P.Messages;

namespace NeoFx.TestNode
{
    class LocalNode : BackgroundService
    {
        private readonly IBlockchain blockchain;
        private readonly IHostApplicationLifetime hostApplicationLifetime;
        private readonly ILogger<LocalNode> log;
        private readonly IRemoteNodeManager remoteNodeManager;
        private readonly Channel<(IRemoteNode node, Message message)> channel = Channel.CreateUnbounded<(IRemoteNode node, Message msg)>();

        public LocalNode(IBlockchain blockchain, IHostApplicationLifetime hostApplicationLifetime, ILogger<LocalNode> logger, IRemoteNodeManager remoteNodeManager)
        {
            this.blockchain = blockchain;
            this.hostApplicationLifetime = hostApplicationLifetime;
            log = logger;
            this.remoteNodeManager = remoteNodeManager;
        }

        protected override async Task ExecuteAsync(CancellationToken token)
        {
            log.LogInformation("LocalNode Starting");

            var (index, hash) = await blockchain.GetLastBlockHash();

            await remoteNodeManager.ConnectAsync(channel.Writer, index, hash, token);

            var reader = channel.Reader;
            while (!token.IsCancellationRequested)
            {
                while (reader.TryRead(out var item))
                {
                    await ProcessMessageAsync(item.node, item.message, token);
                }

                if (!await reader.WaitToReadAsync(token))
                {
                    log.LogError("LocalNode Channel Completed Unexpectedly");
                    hostApplicationLifetime.StopApplication();
                    return;
                }
            }
        }

        async Task ProcessMessageAsync(IRemoteNode node, Message message, CancellationToken token)
        {
            await Task.CompletedTask;

            switch (message)
            {
                // case AddrMessage addrMessage:
                //     {
                //         var addresses = addrMessage.Addresses;
                //         log.LogInformation("Received AddrMessage {addressesCount} {node}", addresses.Length, node.RemoteEndPoint);
                //         // AddUnconnectedNodes(addresses.Select(a => a.EndPoint));
                //     }
                //     break;
                // case HeadersMessage headersMessage:
                //     {
                //         var headers = headersMessage.Headers;
                //         log.LogInformation("Received HeadersMessage {headersCount} {node}", headers.Length, node.RemoteEndPoint);

                //         // The Neo docs suggest sending a getblocks message to retrieve a list 
                //         // of block hashes to sync. However, we can calculate the block hashes 
                //         // from the headers in this message without needing the extra round trip

                //         var (index, _) = await blockchain.GetLastBlockHash();
                //         foreach (var batch in headers.Where(h => h.Index > index).Batch(500))
                //         {
                //             var payload = new InventoryPayload(InventoryPayload.InventoryType.Block, batch.Select(h => h.CalculateHash()));
                //             await node.SendGetDataMessage(payload, token);
                //         }
                //     }
                //     break;
                // case InvMessage invMessage when invMessage.Type == InventoryPayload.InventoryType.Block:
                //     {
                //         var hashes = invMessage.Hashes;
                //         log.LogInformation("Received Block InvMessage {count} {node}", hashes.Length, node.RemoteEndPoint);
                //         await node.SendGetDataMessage(invMessage.Payload, token);
                //         // checkBlockGap.Run(token);
                //     }
                //     break;
                // case BlockMessage blockMessage:
                //     {
                //         log.LogInformation("Received BlockMessage {index} {node}", blockMessage.Block.Index, node.RemoteEndPoint);
                //         await blockchain.AddBlock(blockMessage.Block);
                //     }
                //     break;
                default:
                    log.LogInformation("Received {messageType} {node}", message.GetType().Name, node.RemoteEndPoint);
                    break;
            }
        }
    }
}
