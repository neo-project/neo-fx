using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DevHawk.Buffers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NeoFx.P2P.Messages;

namespace NeoFx.P2P
{
    public sealed class NodeConnection : INodeConnection, IDisposable
    {
        private readonly ILogger<NodeConnection> log;
        private readonly PipelineSocket pipelineSocket;
        public uint Magic { get; private set; }
        public VersionPayload VersionPayload { get; private set; }

        public NodeConnection(PipelineSocket pipelineSocket, ILogger<NodeConnection>? logger = null)
        {
            this.pipelineSocket = pipelineSocket;
            log = logger ?? NullLogger<NodeConnection>.Instance;
        }

        public void Dispose()
        {
            pipelineSocket.Dispose();
        }

        private async Task PerformVersionHandshake(uint magic, VersionPayload payload, CancellationToken token)
        {
            async Task<T> ReceiveMessage<T>(CancellationToken token)
                where T : Message
            {
                var message = await this.ReceiveMessage(token);
                if (message is T typedMessage)
                {
                    return typedMessage;
                }

                throw new InvalidOperationException($"Expected {typeof(T).Name} message, received {message.GetType().Name}");
            }

            Magic = magic;

            log.LogDebug("Sending version message");
            await SendVersion(payload, token).ConfigureAwait(false);

            var versionMessage = await ReceiveMessage<VersionMessage>(token).ConfigureAwait(false);
            log.LogDebug("Received version message {startHeight} {userAgent}", versionMessage.StartHeight, versionMessage.UserAgent);

            log.LogDebug("Sending verack message");
            await SendVerAck(token).ConfigureAwait(false);

            var verAckMessage = await ReceiveMessage<VerAckMessage>(token).ConfigureAwait(false);
            log.LogDebug("Received verack message");

            VersionPayload = versionMessage.Payload;
        }
        public async Task ConnectAsync(IPEndPoint endPoint, uint magic, VersionPayload payload, CancellationToken token = default)
        {
            log.LogTrace("ConnectAsync {magic} to {host}:{port}", magic, endPoint.Address, endPoint.Port);
            await pipelineSocket.ConnectAsync(endPoint, token).ConfigureAwait(false);
            await PerformVersionHandshake(magic, payload, token);
        }

        public async Task ConnectAsync(string host, int port, uint magic, VersionPayload payload, CancellationToken token = default)
        {
            log.LogTrace("ConnectAsync {magic} to {host}:{port}", magic, host, port);
            await pipelineSocket.ConnectAsync(host, port, token).ConfigureAwait(false);
            await PerformVersionHandshake(magic, payload, token);
        }

        public async IAsyncEnumerable<Message> ReceiveMessages([EnumeratorCancellation] CancellationToken token = default)
        {
            while (true)
            {
                yield return await ReceiveMessage(token);
            }
        }

        public async Task<Message> ReceiveMessage(CancellationToken token)
        {
            var inputPipe = pipelineSocket.Input;

            while (true)
            {
                var read = await inputPipe.ReadAsync(token).ConfigureAwait(false);
                log.LogDebug("read {length} bytes from pipe {IsCompleted} {IsCanceled}",
                    read.Buffer.Length, read.IsCompleted, read.IsCanceled);
                if (read.IsCompleted || read.IsCanceled || token.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }

                var buffer = read.Buffer;
                if (buffer.Length < MessageHeader.Size)
                {
                    log.LogTrace("Haven't received enough data to read the message header {bufferLength}", buffer.Length);
                    inputPipe.AdvanceTo(buffer.GetPosition(0), buffer.GetPosition(buffer.Length));
                    continue;
                }

                if (!MessageHeader.TryRead(buffer, out var header))
                {
                    throw new Exception("could not parse message header");
                }
                log.LogDebug("Received {command} message header {magic} {length} {checksum}",
                    header.Command, header.Magic, header.Length, header.Checksum);

                var messageLength = MessageHeader.Size + header.Length;
                if (buffer.Length < messageLength)
                {
                    log.LogTrace("Haven't received enough data to read the message payload {bufferNeeded} {bufferLength}",
                        messageLength, buffer.Length);
                    inputPipe.AdvanceTo(buffer.GetPosition(0), buffer.GetPosition(buffer.Length));
                    continue;
                }

                if (header.Magic != Magic)
                {
                    // ignore messages sent with the wrong magic value
                    log.LogWarning("Ignoring message with incorrect magic {expected} {actual}", Magic, header.Magic);
                    inputPipe.AdvanceTo(buffer.GetPosition(messageLength));
                    continue;
                }

                if (Message.TryRead(buffer.Slice(0, messageLength), header, out var message))
                {
                    log.LogDebug("Receive {message}", message.GetType().Name);
                    inputPipe.AdvanceTo(buffer.GetPosition(messageLength));

                    return message;
                }
                else
                {
                    // TODO: save messages that fail to parse for later review
                    log.LogError("Message Parse {command} {length} {checksum}",
                        header.Command, header.Length, header.Checksum);

                    throw new Exception($"could not parse message {header.Command}");
                }
            }
        }

        private ValueTask SendMessage<T>(string command, in T payload, CancellationToken token)
            where T : IPayload<T>
        {
            log.LogDebug("SendMessage {magic} {command} {payload}", Magic, command, typeof(T).Name);

            var output = pipelineSocket.Output;

            var payloadSize = payload.Size;
            var messageSize = MessageHeader.Size + payloadSize;
            var messageMemory = output.GetMemory(messageSize);

            Span<byte> payloadSpan = default;
            if (payloadSize > 0)
            {
                payloadSpan = messageMemory.Slice(MessageHeader.Size, payloadSize).Span;
                var payloadWriter = new BufferWriter<byte>(payloadSpan);
                payload.WriteTo(ref payloadWriter);
                payloadWriter.Commit();
                Debug.Assert(payloadWriter.Span.IsEmpty);
            }

            Span<byte> buffer = stackalloc byte[32];
            HashHelpers.TryHash256(payloadSpan, buffer);
            var checksum = BitConverter.ToUInt32(buffer.Slice(0, 4));

            var headerWriter = new BufferWriter<byte>(messageMemory.Slice(0, MessageHeader.Size).Span);
            headerWriter.WriteLittleEndian(Magic);
            {
                using var commandOwner = MemoryPool<byte>.Shared.Rent(MessageHeader.CommandSize);
                var commandSpan = commandOwner.Memory.Span.Slice(0, MessageHeader.CommandSize);
                commandSpan.Clear();
                Encoding.ASCII.GetBytes(command, commandSpan);
                headerWriter.Write(commandSpan);
            }
            headerWriter.WriteLittleEndian((uint)payloadSpan.Length);
            headerWriter.WriteLittleEndian(checksum);
            headerWriter.Commit();
            Debug.Assert(headerWriter.Span.IsEmpty);

            output.Advance(messageSize);

            // https://github.com/dotnet/runtime/issues/31503#issuecomment-554415966
            var task = output.FlushAsync(token);
            if (task.IsCompletedSuccessfully)
            {
                var _ = task.Result;
                return default;
            }

            log.LogWarning("converting valuetask to task");
            return new ValueTask(task.AsTask());
        }

        private struct NullPayload : IPayload<NullPayload>
        {
            public int Size => 0;

            public void WriteTo(ref BufferWriter<byte> writer)
            {
            }
        }

        private ValueTask SendVersion(in VersionPayload payload, CancellationToken token = default)
            => SendMessage<VersionPayload>(VersionMessage.CommandText, payload, token);

        private ValueTask SendVerAck(CancellationToken token = default)
            => SendMessage<NullPayload>(VerAckMessage.CommandText, default, token);

        public ValueTask SendAddrMessage(in AddrPayload payload, CancellationToken token = default)
            => SendMessage<AddrPayload>(AddrMessage.CommandText, payload, token);

        public ValueTask SendBlockMessage(in BlockPayload payload, CancellationToken token = default)
            => SendMessage<BlockPayload>(BlockMessage.CommandText, payload, token);

        public ValueTask SendConsensusMessage(in ConsensusPayload payload, CancellationToken token = default)
            => SendMessage<ConsensusPayload>(ConsensusMessage.CommandText, payload, token);

        public ValueTask SendGetAddrMessage(CancellationToken token = default)
            => SendMessage<NullPayload>(GetAddrMessage.CommandText, default, token);

        public ValueTask SendGetBlocksMessage(in HashListPayload payload, CancellationToken token = default)
            => SendMessage<HashListPayload>(GetBlocksMessage.CommandText, payload, token);

        public ValueTask SendGetDataMessage(in InventoryPayload payload, CancellationToken token = default)
            => SendMessage<InventoryPayload>(GetDataMessage.CommandText, payload, token);

        public ValueTask SendGetHeadersMessage(in HashListPayload payload, CancellationToken token = default)
            => SendMessage<HashListPayload>(GetHeadersMessage.CommandText, payload, token);

        public ValueTask SendHeadersMessage(in HeadersPayload payload, CancellationToken token = default)
            => SendMessage<HeadersPayload>(HeadersMessage.CommandText, payload, token);

        public ValueTask SendInvMessage(in InventoryPayload payload, CancellationToken token = default)
            => SendMessage<InventoryPayload>(InvMessage.CommandText, payload, token);

        public ValueTask SendPingMessage(in PingPongPayload payload, CancellationToken token = default)
            => SendMessage<PingPongPayload>(PingMessage.CommandText, payload, token);

        public ValueTask SendPongMessage(in PingPongPayload payload, CancellationToken token = default)
            => SendMessage<PingPongPayload>(PongMessage.CommandText, payload, token);

        public ValueTask SendTransactionMessage(in TransactionPayload payload, CancellationToken token = default)
            => SendMessage<TransactionPayload>(TransactionMessage.CommandText, payload, token);
    }
}
