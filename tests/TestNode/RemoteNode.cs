using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using NeoFx.P2P.Messages;
using NeoFx.P2P;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System;
using Microsoft.Extensions.Options;

namespace NeoFx.TestNode
{
    interface IRemoteNode : IDisposable
    {
        ValueTask SendAddrMessage(in AddrPayload payload, CancellationToken token = default);
        ValueTask SendBlockMessage(in BlockPayload payload, CancellationToken token = default);
        ValueTask SendConsensusMessage(in ConsensusPayload payload, CancellationToken token = default);
        ValueTask SendGetAddrMessage(CancellationToken token = default);
        ValueTask SendGetBlocksMessage(in HashListPayload payload, CancellationToken token = default);
        ValueTask SendGetDataMessage(in InventoryPayload payload, CancellationToken token = default);
        ValueTask SendGetHeadersMessage(in HashListPayload payload, CancellationToken token = default);
        ValueTask SendHeadersMessage(in HeadersPayload payload, CancellationToken token = default);
        ValueTask SendInvMessage(in InventoryPayload payload, CancellationToken token = default);
        ValueTask SendPingMessage(in PingPongPayload payload, CancellationToken token = default);
        ValueTask SendPongMessage(in PingPongPayload payload, CancellationToken token = default);
        ValueTask SendTransactionMessage(in TransactionPayload payload, CancellationToken token = default);
    }

    class RemoteNode : IDisposable, IRemoteNode
    {
        private readonly IPipelineSocket pipelineSocket;
        private readonly ILogger<RemoteNode> log;
        private readonly uint magic;
        private readonly string userAgent;

        public RemoteNode(IPipelineSocket pipelineSocket, IOptions<NetworkOptions> networkOptions, IOptions<NodeOptions> nodeOptions, ILogger<RemoteNode>? logger = null)
            : this(pipelineSocket, networkOptions.Value.Magic, nodeOptions.Value.UserAgent, logger)
        {
        }

        public RemoteNode(IPipelineSocket pipelineSocket, uint magic, string userAgent, ILogger<RemoteNode>? logger = null)
        {
            this.pipelineSocket = pipelineSocket;
            this.magic = magic;
            this.userAgent = userAgent;
            log = logger ?? NullLogger<RemoteNode>.Instance;
        }

        public void Dispose()
        {
            pipelineSocket.Dispose();
        }

        public async ValueTask<VersionPayload> ConnectAsync(IPEndPoint endPoint, uint nonce, uint startHeight, ChannelWriter<Message> writer, CancellationToken token = default)
        {
            var localVersion = new VersionPayload(nonce, userAgent, startHeight);

            log.LogTrace("ConnectAsync {magic} to {host}:{port}", magic, endPoint.Address, endPoint.Port);
            await pipelineSocket.ConnectAsync(endPoint, token).ConfigureAwait(false);
            var remoteVersion = await NodeOperations.PerformVersionHandshake(pipelineSocket, magic, localVersion, log, token);
            log.LogInformation("Connected to {endpoint} {userAgent}", pipelineSocket.RemoteEndPoint, remoteVersion.UserAgent);
            Execute(writer, token);
            return remoteVersion;
        }

        private void Execute(ChannelWriter<Message> writer, CancellationToken token)
        {
            MessageReceiveAsync(writer, token)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        log.LogError(t.Exception, nameof(RemoteNode) + " exception");
                    }
                    else
                    {
                        log.LogInformation(nameof(RemoteNode) + " completed {IsCanceled}", t.IsCanceled);
                    }
                    Dispose();
                });
        }

        private async Task MessageReceiveAsync(ChannelWriter<Message> writer, CancellationToken token)
        {
            while (true)
            {
                var message = await NodeOperations.ReceiveMessage(pipelineSocket.Input, magic, log, token);
                if (log.IsEnabled(LogLevel.Trace)) log.LogTrace("{} message received", message.GetType().Name);

                while (!writer.TryWrite(message))
                {
                    // if WaitToWriteAsync returns false, the channel has been closed  
                    if (!await writer.WaitToWriteAsync(token)) 
                    {
                        return;
                    }
                }
            }
        }

        public ValueTask SendAddrMessage(in AddrPayload payload, CancellationToken token = default)
            => NodeOperations.SendMessage<AddrPayload>(pipelineSocket.Output, magic, AddrMessage.CommandText, payload, log, token);

        public ValueTask SendBlockMessage(in BlockPayload payload, CancellationToken token = default)
            => NodeOperations.SendMessage<BlockPayload>(pipelineSocket.Output, magic, BlockMessage.CommandText, payload, log, token);

        public ValueTask SendConsensusMessage(in ConsensusPayload payload, CancellationToken token = default)
            => NodeOperations.SendMessage<ConsensusPayload>(pipelineSocket.Output, magic, ConsensusMessage.CommandText, payload, log, token);

        public ValueTask SendGetAddrMessage(CancellationToken token = default)
            => NodeOperations.SendEmptyMessage(pipelineSocket.Output, magic, GetAddrMessage.CommandText, log, token);

        public ValueTask SendGetBlocksMessage(in HashListPayload payload, CancellationToken token = default)
            => NodeOperations.SendMessage<HashListPayload>(pipelineSocket.Output, magic, GetBlocksMessage.CommandText, payload, log, token);

        public ValueTask SendGetDataMessage(in InventoryPayload payload, CancellationToken token = default)
            => NodeOperations.SendMessage<InventoryPayload>(pipelineSocket.Output, magic, GetDataMessage.CommandText, payload, log, token);

        public ValueTask SendGetHeadersMessage(in HashListPayload payload, CancellationToken token = default)
            => NodeOperations.SendMessage<HashListPayload>(pipelineSocket.Output, magic, GetHeadersMessage.CommandText, payload, log, token);

        public ValueTask SendHeadersMessage(in HeadersPayload payload, CancellationToken token = default)
            => NodeOperations.SendMessage<HeadersPayload>(pipelineSocket.Output, magic, HeadersMessage.CommandText, payload, log, token);

        public ValueTask SendInvMessage(in InventoryPayload payload, CancellationToken token = default)
            => NodeOperations.SendMessage<InventoryPayload>(pipelineSocket.Output, magic, InvMessage.CommandText, payload, log, token);

        public ValueTask SendPingMessage(in PingPongPayload payload, CancellationToken token = default)
            => NodeOperations.SendMessage<PingPongPayload>(pipelineSocket.Output, magic, PingMessage.CommandText, payload, log, token);

        public ValueTask SendPongMessage(in PingPongPayload payload, CancellationToken token = default)
            => NodeOperations.SendMessage<PingPongPayload>(pipelineSocket.Output, magic, PongMessage.CommandText, payload, log, token);

        public ValueTask SendTransactionMessage(in TransactionPayload payload, CancellationToken token = default)
            => NodeOperations.SendMessage<TransactionPayload>(pipelineSocket.Output, magic, TransactionMessage.CommandText, payload, log, token);
    }
}
