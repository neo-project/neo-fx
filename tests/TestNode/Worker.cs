using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoFx.Models;
using NeoFx.P2P.Messages;

namespace NeoFx.TestNode
{
    class Worker : BackgroundService
    {
        private readonly IHostApplicationLifetime hostApplicationLifetime;
        private readonly ILogger<Worker> log;
        private readonly NetworkOptions networkOptions;
        private readonly NodeOptions nodeOptions;
        private readonly INodeConnectionFactory nodeConnectionFactory;
        private readonly IHeaderStorage headerStorage;

        public Worker(INodeConnectionFactory nodeConnectionFactory,
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
            this.nodeConnectionFactory = nodeConnectionFactory;
            this.headerStorage = headerStorage;

            log.LogInformation("header storage {type} {count}", headerStorage.GetType().Name, headerStorage.Count);
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
                var magic = networkOptions.Magic;
                var nodeConnection = nodeConnectionFactory.CreateConnection(magic);

                var (address, port) = networkOptions.GetRandomSeed();
                var localVersionPayload = new VersionPayload(GetNonce(), nodeOptions.UserAgent);

                log.LogInformation("Connecting to {address}:{port} {magic}", address, port, magic);
                var remoteVersionPayload = await nodeConnection.ConnectAsync(address, port, localVersionPayload, token);
                log.LogInformation("Connected to {userAgent}", remoteVersionPayload.UserAgent);

                await nodeConnection.SendGetAddrMessage(token);

                UInt256 lastHash;
                if (headerStorage.TryGetLastHash(out lastHash))
                {
                    await nodeConnection.SendGetBlocksMessage(new HashListPayload(lastHash), token).ConfigureAwait(false);
                    // await nodeConnection.SendGetHeadersMessage(new HashListPayload(lastHash)).ConfigureAwait(false);
                }

                await foreach (var msg in nodeConnection.ReceiveMessages(token))
                {
                    if (token.IsCancellationRequested) break;
                    Debug.Assert(msg.Magic == Magic);

                    switch (msg)
                    {
                        case AddrMessage addrMessage:
                            {
                                log.LogInformation("Received AddrMessage {addressCount}", addrMessage.Addresses.Length);
                            }
                            break;
                        case HeadersMessage headersMessage:
                            {
                                AddRange(headerStorage, headersMessage.Headers.AsSpan());
                                log.LogInformation("Received HeadersMessage {messageCount} {totalCount}", headersMessage.Headers.Length, headerStorage.Count);

                                if (headerStorage.TryGetLastHash(out lastHash))
                                {
                                    await nodeConnection.SendGetBlocksMessage(new HashListPayload(lastHash), token).ConfigureAwait(false);
                                }
                            }
                            break;
                        case InvMessage invMessage:
                            {
                                log.LogInformation("Received InvMessage {type} {count}", invMessage.Type, invMessage.Hashes.Length);
                                if (invMessage.Type == InventoryPayload.InventoryType.Block)
                                {
                                    await nodeConnection.SendGetDataMessage(invMessage.Payload, token).ConfigureAwait(false);
                                }
                            }
                            break;
                        case BlockMessage blockMessage:
                            {
                                var block = blockMessage.Block;
                                log.LogInformation("Received Block Message {index} {txCount}", block.Index, block.Transactions.Length);
                            }
                            break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // ignore operation canceled exceptions
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    log.LogError(ex, string.Empty);
                }
            }
            finally
            {
                hostApplicationLifetime.StopApplication();
            }
        }
    }
}
