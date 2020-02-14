using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using NeoFx.P2P.Messages;
using NeoFx.P2P;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace NeoFx.TestNode
{
    class RemoteNode : IRemoteNode
    {
        private readonly ChannelWriter<(IRemoteNode, Message)> writer;
        private readonly INodeConnection connection;
        private readonly ILogger<RemoteNode> log;
        public VersionPayload VersionPayload { get; private set; }
        public EndPoint RemoteEndPoint => connection.RemoteEndPoint;

        public RemoteNode(INodeConnection connection, ChannelWriter<(IRemoteNode, Message)> writer, ILogger<RemoteNode>? logger = null)
        {
            this.connection = connection;
            this.writer = writer;
            log = logger ?? NullLogger<RemoteNode>.Instance;
        }

        public async Task Connect(IPEndPoint endPoint, VersionPayload payload, CancellationToken token = default)
        {
            log.LogInformation("Connecting to {address}:{port}", endPoint.Address, endPoint.Port);
            VersionPayload = await connection.ConnectAsync(endPoint, payload, token);
            Execute(token);
        }

        private void Execute(CancellationToken token)
        {
            ExecuteAsync(token).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        log.LogError(t.Exception, nameof(RemoteNode) + " exception");
                    }
                    else
                    {
                        log.LogInformation(nameof(RemoteNode) + " completed {IsCanceled}", t.IsCanceled);
                    }
                    writer.Complete(t.Exception);
                });
        }

        private async Task ExecuteAsync(CancellationToken token)
        {
            log.LogInformation("Connected to {userAgent}", VersionPayload.UserAgent);

            while (true)
            {
                var message = await connection.ReceiveMessage(token);
                if (log.IsEnabled(LogLevel.Trace)) log.LogTrace("{} message received", message.GetType().Name);
                await writer.WriteAsync((this, message), token);
            }
        }

        public ValueTask SendAddrMessage(in AddrPayload payload, CancellationToken token = default) => connection.SendAddrMessage(payload, token);
        public ValueTask SendBlockMessage(in BlockPayload payload, CancellationToken token = default) => connection.SendBlockMessage(payload, token);
        public ValueTask SendConsensusMessage(in ConsensusPayload payload, CancellationToken token = default) => connection.SendConsensusMessage(payload, token);
        public ValueTask SendGetAddrMessage(CancellationToken token = default) => connection.SendGetAddrMessage(token);
        public ValueTask SendGetBlocksMessage(in HashListPayload payload, CancellationToken token = default) => connection.SendGetBlocksMessage(payload, token);
        public ValueTask SendGetDataMessage(in InventoryPayload payload, CancellationToken token = default) => connection.SendGetDataMessage(payload, token);
        public ValueTask SendGetHeadersMessage(in HashListPayload payload, CancellationToken token = default) => connection.SendGetHeadersMessage(payload, token);
        public ValueTask SendHeadersMessage(in HeadersPayload payload, CancellationToken token = default) => connection.SendHeadersMessage(payload, token);
        public ValueTask SendInvMessage(in InventoryPayload payload, CancellationToken token = default) => connection.SendInvMessage(payload, token);
        public ValueTask SendPingMessage(in PingPongPayload payload, CancellationToken token = default) => connection.SendPingMessage(payload, token);
        public ValueTask SendPongMessage(in PingPongPayload payload, CancellationToken token = default) => connection.SendPongMessage(payload, token);
        public ValueTask SendTransactionMessage(in TransactionPayload payload, CancellationToken token = default) => connection.SendTransactionMessage(payload, token);
    }
}
