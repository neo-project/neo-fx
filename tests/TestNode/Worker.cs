using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoFx.Models;
using NeoFx.P2P;
using NeoFx.P2P.Messages;
using NeoFx.TestNode.Options;

namespace NeoFx.TestNode
{
    class Worker : BackgroundService
    {
        private readonly IHostApplicationLifetime hostApplicationLifetime;
        private readonly ILogger<Worker> log;
        private readonly NetworkOptions networkOptions;
        private readonly NodeOptions nodeOptions;
        private readonly NeoClient neoClient;
        private readonly IHeaderStorage headerStorage;

        public Worker(NeoClient neoClient,
                      IHostApplicationLifetime hostApplicationLifetime,
                      ILogger<Worker> log,
                      IOptions<NetworkOptions> networkOptions,
                      IOptions<NodeOptions> nodeOptions,
                      IHeaderStorage headerStorage)
        {
            this.hostApplicationLifetime = hostApplicationLifetime;
            this.log = log;
            this.networkOptions = networkOptions.Value;
            this.nodeOptions = nodeOptions.Value;
            this.neoClient = neoClient;
            this.headerStorage = headerStorage;

            if (headerStorage.Count == 0)
            {
                var genesisBlock = Genesis.CreateGenesisBlock(this.networkOptions.GetValidators());
                headerStorage.Add(genesisBlock.Header);
            }
        }

        private uint Magic => networkOptions.Magic;

        public override void Dispose()
        {
            neoClient.Dispose();
            base.Dispose();
        }

        private static uint GetNonce()
        {
            var random = new Random();
            Span<byte> span = stackalloc byte[4];
            random.NextBytes(span);
            return System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(span);
        }

        async Task<T> ReceiveMessage<T>(CancellationToken token)
            where T : Message
        {
            var message = await neoClient.GetMessage(token).ConfigureAwait(false);
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
            static void AddRange(IHeaderStorage headerStorage, ReadOnlySpan<BlockHeader> headers)
            {
                for (var i = 0; i < headers.Length; i++)
                {
                    headerStorage.Add(headers[i]);
                }
            }

            try
            {
                var (address, port) = networkOptions.GetRandomSeed();
                await neoClient.ConnectAsync(address, port, token).ConfigureAwait(false);

                if (token.IsCancellationRequested) return;
                log.LogInformation("Sending version message {magic}", Magic);
                await neoClient.SendVersion(Magic, new VersionPayload(GetNonce(), nodeOptions.UserAgent)).ConfigureAwait(false);

                if (token.IsCancellationRequested) return;
                var verMsg = await ReceiveMessage<VersionMessage>(token);
                log.LogInformation("Received version message {startHeight} {userAgent}", verMsg.StartHeight, verMsg.UserAgent);

                if (token.IsCancellationRequested) return;
                log.LogInformation("Sending verack message {magic}", Magic);
                await neoClient.SendVerAck(Magic).ConfigureAwait(false);

                if (token.IsCancellationRequested) return;
                var verAck = await ReceiveMessage<VerAckMessage>(token);
                log.LogInformation("Received verack message");

                {
                    if (headerStorage.TryGetLastHash(out var lastHash))
                    {
                        await neoClient.SendGetHeaders(Magic, new HashListPayload(lastHash)).ConfigureAwait(false);
                    }
                }

                await foreach (var msg in neoClient.GetMessages(token))
                {
                    if (token.IsCancellationRequested) break;
                    Debug.Assert(msg.Magic == Magic);

                    switch (msg)
                    {
                        case InvMessage invMessage:
                            log.LogInformation("Received InvMessage {type} {count}", invMessage.Type, invMessage.Hashes.Length);
                            break;
                        case HeadersMessage headersMessage:
                            {
                                AddRange(headerStorage, headersMessage.Headers.AsSpan());
                                log.LogInformation("Received HeadersMessage {messageCount} {totalCount}", headersMessage.Headers.Length, headerStorage.Count);

                                if (headerStorage.TryGetLastHash(out var lastHash))
                                {
                                    await neoClient.SendGetHeaders(Magic, new HashListPayload(lastHash)).ConfigureAwait(false);
                                }
                            }
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
