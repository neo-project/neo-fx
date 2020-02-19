using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NeoFx.P2P.Messages;

namespace NeoFx.TestNode
{
    public sealed class NodeConnection : IDisposable
    {
        private readonly ILogger<NodeConnection> log;
        private readonly PipelineSocket pipelineSocket;
        private readonly uint magic;

        public EndPoint RemoteEndPoint => pipelineSocket.RemoteEndPoint;

        public NodeConnection(PipelineSocket pipelineSocket, uint magic, ILogger<NodeConnection>? logger = null)
        {
            this.pipelineSocket = pipelineSocket;
            this.magic = magic;
            log = logger ?? NullLogger<NodeConnection>.Instance;
        }

        public void Dispose()
        {
            pipelineSocket.Dispose();
        }

        public async Task<VersionPayload> ConnectAsync(IPEndPoint endPoint, VersionPayload payload, CancellationToken token = default)
        {
            log.LogTrace("ConnectAsync {magic} to {host}:{port}", magic, endPoint.Address, endPoint.Port);
            await pipelineSocket.ConnectAsync(endPoint, token).ConfigureAwait(false);
            return await NodeOperations.PerformVersionHandshake(pipelineSocket, magic, payload, log, token);
        }

        public ValueTask<Message> ReceiveMessage(CancellationToken token)
        {
            return NodeOperations.ReceiveMessage(pipelineSocket.Input, magic, log, token);
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
