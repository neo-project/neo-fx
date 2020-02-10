using System;
using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DevHawk.Buffers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NeoFx.P2P.Messages;
using NeoFx.Storage;

namespace NeoFx.P2P
{
    public sealed class NodeConnection : INodeConnection, IDisposable
    {
        private readonly ILogger<NodeConnection> log;
        private readonly PipelineSocket pipelineSocket;

        public NodeConnection(PipelineSocket pipelineSocket, ILogger<NodeConnection>? logger = null)
        {
            this.pipelineSocket = pipelineSocket;
            log = logger ?? NullLogger<NodeConnection>.Instance;
        }

        public void Dispose()
        {
            pipelineSocket.Dispose();
        }

        private async Task<VersionPayload> PerformVersionHandshake(uint magic, VersionPayload payload, CancellationToken token)
        {
            async Task<T> ReceiveMessage<T>(CancellationToken token)
                where T : Message
            {
                var message = await this.ReceiveMessage(magic, token);
                if (message is T typedMessage)
                {
                    return typedMessage;
                }

                throw new InvalidOperationException($"Expected {typeof(T).Name} message, received {message.GetType().Name}");
            }

            log.LogDebug("Sending version message");
            await SendVersion(magic, payload, token).ConfigureAwait(false);

            var versionMessage = await ReceiveMessage<VersionMessage>(token).ConfigureAwait(false);
            log.LogDebug("Received version message {startHeight} {userAgent}", versionMessage.StartHeight, versionMessage.UserAgent);

            log.LogDebug("Sending verack message");
            await SendVerAck(magic, token).ConfigureAwait(false);

            var verAckMessage = await ReceiveMessage<VerAckMessage>(token).ConfigureAwait(false);
            log.LogDebug("Received verack message");

            return versionMessage.Payload;
        }

        public async Task<VersionPayload> ConnectAsync(IPEndPoint endPoint, uint magic, VersionPayload payload, CancellationToken token = default)
        {
            log.LogTrace("ConnectAsync {magic} to {host}:{port}", magic, endPoint.Address, endPoint.Port);
            await pipelineSocket.ConnectAsync(endPoint, token).ConfigureAwait(false);
            return await PerformVersionHandshake(magic, payload, token);
        }

        public async Task<VersionPayload> ConnectAsync(string host, int port, uint magic, VersionPayload payload, CancellationToken token = default)
        {
            log.LogTrace("ConnectAsync {magic} to {host}:{port}", magic, host, port);
            await pipelineSocket.ConnectAsync(host, port, token).ConfigureAwait(false);
            return await PerformVersionHandshake(magic, payload, token);
        }

        public async ValueTask<Message> ReceiveMessage(uint magic, CancellationToken token)
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

                if (header.Magic != magic)
                {
                    // ignore messages sent with the wrong magic value
                    log.LogWarning("Ignoring message with incorrect magic {expected} {actual}", magic, header.Magic);
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

        private ValueTask SendMessage<T>(uint magic, string command, in T payload, CancellationToken token)
            where T : IWritable<T>
        {
            log.LogDebug("SendMessage {magic} {command} {payload}", magic, command, typeof(T).Name);

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
            headerWriter.WriteLittleEndian(magic);
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

            return new ValueTask(task.AsTask());
        }

        private struct NullPayload : IWritable<NullPayload>
        {
            public int Size => 0;

            public void WriteTo(ref BufferWriter<byte> writer)
            {
            }
        }

        private ValueTask SendVersion(uint magic, in VersionPayload payload, CancellationToken token = default)
            => SendMessage<VersionPayload>(magic, VersionMessage.CommandText, payload, token);

        private ValueTask SendVerAck(uint magic, CancellationToken token = default)
            => SendMessage<NullPayload>(magic, VerAckMessage.CommandText, default, token);

        public ValueTask SendAddrMessage(uint magic, in AddrPayload payload, CancellationToken token = default)
            => SendMessage<AddrPayload>(magic, AddrMessage.CommandText, payload, token);

        public ValueTask SendBlockMessage(uint magic, in BlockPayload payload, CancellationToken token = default)
            => SendMessage<BlockPayload>(magic, BlockMessage.CommandText, payload, token);

        public ValueTask SendConsensusMessage(uint magic, in ConsensusPayload payload, CancellationToken token = default)
            => SendMessage<ConsensusPayload>(magic, ConsensusMessage.CommandText, payload, token);

        public ValueTask SendGetAddrMessage(uint magic, CancellationToken token = default)
            => SendMessage<NullPayload>(magic, GetAddrMessage.CommandText, default, token);

        public ValueTask SendGetBlocksMessage(uint magic, in HashListPayload payload, CancellationToken token = default)
            => SendMessage<HashListPayload>(magic, GetBlocksMessage.CommandText, payload, token);

        public ValueTask SendGetDataMessage(uint magic, in InventoryPayload payload, CancellationToken token = default)
            => SendMessage<InventoryPayload>(magic, GetDataMessage.CommandText, payload, token);

        public ValueTask SendGetHeadersMessage(uint magic, in HashListPayload payload, CancellationToken token = default)
            => SendMessage<HashListPayload>(magic, GetHeadersMessage.CommandText, payload, token);

        public ValueTask SendHeadersMessage(uint magic, in HeadersPayload payload, CancellationToken token = default)
            => SendMessage<HeadersPayload>(magic, HeadersMessage.CommandText, payload, token);

        public ValueTask SendInvMessage(uint magic, in InventoryPayload payload, CancellationToken token = default)
            => SendMessage<InventoryPayload>(magic, InvMessage.CommandText, payload, token);

        public ValueTask SendPingMessage(uint magic, in PingPongPayload payload, CancellationToken token = default)
            => SendMessage<PingPongPayload>(magic, PingMessage.CommandText, payload, token);

        public ValueTask SendPongMessage(uint magic, in PingPongPayload payload, CancellationToken token = default)
            => SendMessage<PingPongPayload>(magic, PongMessage.CommandText, payload, token);

        public ValueTask SendTransactionMessage(uint magic, in TransactionPayload payload, CancellationToken token = default)
            => SendMessage<TransactionPayload>(magic, TransactionMessage.CommandText, payload, token);
    }
}
