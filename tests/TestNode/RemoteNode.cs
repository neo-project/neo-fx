using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using NeoFx.P2P.Messages;
using NeoFx.P2P;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace NeoFx.TestNode
{

    class RemoteNode : IRemoteNode
    {
        private readonly ChannelWriter<(IRemoteNode, Message)> writer;
        private readonly INodeConnection connection;
        private readonly ILogger<RemoteNode> log;
        public VersionPayload VersionPayload { get; private set; }

        public RemoteNode(INodeConnectionFactory connectionFactory, ChannelWriter<(IRemoteNode, Message)> writer, ILogger<RemoteNode>? logger = null)
        {
            this.connection = connectionFactory.CreateConnection();
            this.writer = writer;
            log = logger ?? NullLogger<RemoteNode>.Instance;
        }

        public void Connect(string address, int port, in VersionPayload version, CancellationToken token = default)
        {
            log.LogInformation("Connecting to {address}:{port}", address, port);
            Connect(Execute(address, port, version, token));
        }

        public void Connect(IPEndPoint endPoint, in VersionPayload version, CancellationToken token = default)
        {
            log.LogInformation("Connecting to {address}:{port}", endPoint.Address, endPoint.Port);
            Connect(Execute(endPoint, version, token));
        }

        private void Connect(Task task)
        {
            task.ContinueWith(t =>
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

        private async Task Execute(string address, int port, VersionPayload payload, CancellationToken token)
        {
            VersionPayload = await connection.ConnectAsync(address, port, payload, token);
            await Execute(token);
        }

        private async Task Execute(IPEndPoint endPoint, VersionPayload payload, CancellationToken token)
        {
            VersionPayload = await connection.ConnectAsync(endPoint, payload, token);
            await Execute(token);
        }

        private async Task Execute(CancellationToken token)
        {
            log.LogInformation("Connected to {userAgent}", VersionPayload.UserAgent);

            // TODO: remove SendGetAddrMessage
            await connection.SendGetAddrMessage(token);

            while (true)
            {
                var message = await connection.ReceiveMessage(token);
                if (log.IsEnabled(LogLevel.Trace)) log.LogTrace("{} message received", message.GetType().Name);
                await writer.WriteAsync((this, message), token);
            }
        }
    }
}
