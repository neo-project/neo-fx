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
        EndPoint RemoteEndPoint { get; }
        ValueTask<Message?> ReceiveMessage(CancellationToken token = default);
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

        public EndPoint RemoteEndPoint => pipelineSocket.RemoteEndPoint;

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

        public async Task<VersionPayload> ConnectAsync(IPEndPoint endpoint, uint nonce, uint startHeight, CancellationToken token = default)
        {
            var localVersion = new VersionPayload(nonce, userAgent, startHeight);

            log.LogTrace("ConnectAsync {magic} to {endpoint}", magic, endpoint);
            await pipelineSocket.ConnectAsync(endpoint, token).ConfigureAwait(false);
            var remoteVersion = await NodeOperations.PerformVersionHandshake(pipelineSocket, endpoint, magic, localVersion, log, token);
            log.LogInformation("Connected to {endpoint} {userAgent}", pipelineSocket.RemoteEndPoint, remoteVersion.UserAgent);
            return remoteVersion;
        }

        public ValueTask<Message?> ReceiveMessage(CancellationToken token = default)
            => NodeOperations.ReceiveMessage(pipelineSocket.Input, pipelineSocket.RemoteEndPoint, magic, log, token);

        public ValueTask SendAddrMessage(in AddrPayload payload, CancellationToken token = default)
            => NodeOperations
                .SendMessage<AddrPayload>(pipelineSocket.Output, magic, AddrMessage.CommandText, payload, log, token)
                .AsValueTask();

        public ValueTask SendBlockMessage(in BlockPayload payload, CancellationToken token = default)
            => NodeOperations
                .SendMessage<BlockPayload>(pipelineSocket.Output, magic, BlockMessage.CommandText, payload, log, token)
                .AsValueTask();

        public ValueTask SendConsensusMessage(in ConsensusPayload payload, CancellationToken token = default)
            => NodeOperations
                .SendMessage<ConsensusPayload>(pipelineSocket.Output, magic, ConsensusMessage.CommandText, payload, log, token)
                .AsValueTask();

        public ValueTask SendGetAddrMessage(CancellationToken token = default)
            => NodeOperations
                .SendMessage(pipelineSocket.Output, magic, GetAddrMessage.CommandText, log, token)
                .AsValueTask();

        public ValueTask SendGetBlocksMessage(in HashListPayload payload, CancellationToken token = default)
            => NodeOperations
                .SendMessage<HashListPayload>(pipelineSocket.Output, magic, GetBlocksMessage.CommandText, payload, log, token)
                .AsValueTask();

        public ValueTask SendGetDataMessage(in InventoryPayload payload, CancellationToken token = default)
            => NodeOperations
                .SendMessage<InventoryPayload>(pipelineSocket.Output, magic, GetDataMessage.CommandText, payload, log, token)
                .AsValueTask();

        public ValueTask SendGetHeadersMessage(in HashListPayload payload, CancellationToken token = default)
            => NodeOperations
                .SendMessage<HashListPayload>(pipelineSocket.Output, magic, GetHeadersMessage.CommandText, payload, log, token)
                .AsValueTask();

        public ValueTask SendHeadersMessage(in HeadersPayload payload, CancellationToken token = default)
            => NodeOperations
                .SendMessage<HeadersPayload>(pipelineSocket.Output, magic, HeadersMessage.CommandText, payload, log, token)
                .AsValueTask();

        public ValueTask SendInvMessage(in InventoryPayload payload, CancellationToken token = default)
            => NodeOperations
                .SendMessage<InventoryPayload>(pipelineSocket.Output, magic, InvMessage.CommandText, payload, log, token)
                .AsValueTask();
        public ValueTask SendPingMessage(in PingPongPayload payload, CancellationToken token = default)
            => NodeOperations
                .SendMessage<PingPongPayload>(pipelineSocket.Output, magic, PingMessage.CommandText, payload, log, token)
                .AsValueTask();

        public ValueTask SendPongMessage(in PingPongPayload payload, CancellationToken token = default)
            => NodeOperations
                .SendMessage<PingPongPayload>(pipelineSocket.Output, magic, PongMessage.CommandText, payload, log, token)
                .AsValueTask();
        public ValueTask SendTransactionMessage(in TransactionPayload payload, CancellationToken token = default)
            => NodeOperations
                .SendMessage<TransactionPayload>(pipelineSocket.Output, magic, TransactionMessage.CommandText, payload, log, token)
                .AsValueTask();
    }
}
