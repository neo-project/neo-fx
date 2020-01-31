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
        private readonly INodeConnection nodeConnection;
        private readonly IHeaderStorage headerStorage;

        public Worker(INodeConnection nodeConnection,
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
            this.nodeConnection = nodeConnection;
            this.headerStorage = headerStorage;

            if (headerStorage.Count == 0)
            {
                var genesisBlock = Genesis.CreateGenesisBlock(this.networkOptions.GetValidators());
                headerStorage.Add(genesisBlock.Header);
            }
        }

        private uint Magic => networkOptions.Magic;

        private static uint GetNonce()
        {
            var random = new Random();
            Span<byte> span = stackalloc byte[4];
            random.NextBytes(span);
            return System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(span);
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
                var versionPayload = new VersionPayload(GetNonce(), nodeOptions.UserAgent);

                log.LogInformation("Connecting to {address}:{port} {magic}", address, port, networkOptions.Magic);
                await nodeConnection.ConnectAsync(address, port, networkOptions.Magic, versionPayload);
                log.LogInformation("Connected to {userAgent}", nodeConnection.VersionPayload.UserAgent);

                {
                    if (headerStorage.TryGetLastHash(out var lastHash))
                    {
                        await nodeConnection.SendGetHeadersMessage(new HashListPayload(lastHash)).ConfigureAwait(false);
                    }
                }

                await foreach (var msg in nodeConnection.ReceiveMessages(token))
                {
                    if (token.IsCancellationRequested) break;
                    Debug.Assert(msg.Magic == Magic);

                    switch (msg)
                    {
                        // case InvMessage invMessage:
                        //     log.LogInformation("Received InvMessage {type} {count}", invMessage.Type, invMessage.Hashes.Length);
                        //     break;
                        case HeadersMessage headersMessage:
                            {
                                AddRange(headerStorage, headersMessage.Headers.AsSpan());
                                log.LogInformation("Received HeadersMessage {messageCount} {totalCount}", headersMessage.Headers.Length, headerStorage.Count);

                                if (headerStorage.TryGetLastHash(out var lastHash))
                                {
                                    await nodeConnection.SendGetHeadersMessage(new HashListPayload(lastHash)).ConfigureAwait(false);
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
