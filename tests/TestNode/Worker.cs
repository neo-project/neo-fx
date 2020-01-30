using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoFx;
using NeoFx.P2P;
using NeoFx.P2P.Messages;
using NeoFx.TestNode.Options;

namespace NeoFx.TestNode
{
    internal class Worker : BackgroundService
    {
        private readonly IHostApplicationLifetime hostApplicationLifetime;
        private readonly ILogger<Worker> log;
        private readonly ILoggerFactory loggerFactory;
        private readonly NetworkOptions networkOptions;
        private readonly NodeOptions nodeOptions;
        private readonly PipelineSocket pipelineSocket;

        public Worker(IHostApplicationLifetime hostApplicationLifetime, ILoggerFactory loggerFactory, IOptions<NetworkOptions> networkOptions, IOptions<NodeOptions> nodeOptions)
        {
            this.hostApplicationLifetime = hostApplicationLifetime;
            this.loggerFactory = loggerFactory;
            this.networkOptions = networkOptions.Value;
            this.nodeOptions = nodeOptions.Value;

            log = loggerFactory?.CreateLogger<Worker>() ?? NullLogger<Worker>.Instance;
            pipelineSocket = new PipelineSocket(loggerFactory);
        }

        private uint Magic => networkOptions.Magic;

        public override void Dispose()
        {
            pipelineSocket.Dispose();
            base.Dispose();
        }

        private static uint GetNonce()
        {
            var random = new Random();
            Span<byte> span = stackalloc byte[4];
            random.NextBytes(span);
            return System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(span);
        }

        async Task<T> ReceiveMessage<T>(NeoClient client, CancellationToken token)
            where T : Message
        {
            var message = await client.GetMessage(token).ConfigureAwait(false);
            if (message == null) 
            {
                log.LogError("Expected {} received nothing", typeof(T).Name);
                throw new Exception();
            }

            Debug.Assert(message.Magic == Magic);
            if (message is T tMessage)
            {
                return tMessage;
            }
            else
            {
                log.LogError("Expected {} received {command}", typeof(T).Name, message.Command);
                throw new Exception();
            }
        }
        protected override async Task ExecuteAsync(CancellationToken token)
        {
            try
            {
                var (address, port) = networkOptions.GetRandomSeed();
                await pipelineSocket.ConnectAsync(address, port, token).ConfigureAwait(false);

                var client = new NeoClient(pipelineSocket, loggerFactory);
                
                if (token.IsCancellationRequested) return;
                log.LogInformation("Sending version message {magic}", Magic);
                await client.SendVersion(Magic, new VersionPayload(GetNonce(), nodeOptions.UserAgent)).ConfigureAwait(false);

                if (token.IsCancellationRequested) return;
                var verMsg = await ReceiveMessage<VersionMessage>(client, token);
                log.LogInformation("Received version message {startHeight} {userAgent}", verMsg.StartHeight, verMsg.UserAgent);

                if (token.IsCancellationRequested) return;
                log.LogInformation("Sending verack message {magic}", Magic);
                await client.SendVerAck(Magic).ConfigureAwait(false);

                if (token.IsCancellationRequested) return;
                var verAck = await ReceiveMessage<VerAckMessage>(client, token);
                log.LogInformation("Received verack message");

                await foreach (var msg in client.GetMessages(token))
                {
                    if (token.IsCancellationRequested) break;
                    Debug.Assert(msg.Magic == Magic);

                    switch (msg)
                    {
                        case InvMessage invMessage:
                            log.LogInformation("Received InvMessage {type} {count}", invMessage.Type, invMessage.Hashes.Length);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, string.Empty);
            }
            finally
            {
                hostApplicationLifetime.StopApplication();
            }
        }
    }
}
