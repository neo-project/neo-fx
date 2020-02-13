using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DevHawk.Buffers;
using Microsoft.Extensions.Logging;
using NeoFx.P2P.Messages;
using NeoFx.Storage;

namespace NeoFx.P2P
{
    public static class NodeOperations
    {
        static uint CalculateChecksum(ReadOnlySequence<byte> sequence)
        {
            Span<byte> hashBuffer = stackalloc byte[32];
            HashHelpers.TryHash256(sequence, hashBuffer);
            return BitConverter.ToUInt32(hashBuffer.Slice(0, 4));
        }

        private static Message? ReceiveMessage(ReadResult readResult, PipeReader reader, uint magic, ILogger log, CancellationToken token = default)
        {
            log.LogDebug("read {length} bytes from pipe {IsCompleted} {IsCanceled}",
                readResult.Buffer.Length, readResult.IsCompleted, readResult.IsCanceled);
            if (readResult.IsCompleted || readResult.IsCanceled || token.IsCancellationRequested)
            {
                throw new OperationCanceledException();
            }

            var buffer = readResult.Buffer;
            if (buffer.Length < MessageHeader.Size)
            {
                log.LogTrace("Haven't received enough data to read the message header {bufferLength}", buffer.Length);
                reader.AdvanceTo(buffer.GetPosition(0), buffer.GetPosition(buffer.Length));
                return null;
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
                reader.AdvanceTo(buffer.GetPosition(0), buffer.GetPosition(buffer.Length));
                return null;
            }

            if (header.Magic != magic)
            {
                // ignore messages sent with the wrong magic value
                log.LogWarning("Ignoring message with incorrect magic {expected} {actual}", magic, header.Magic);
                reader.AdvanceTo(buffer.GetPosition(messageLength));
                return null;
            }

            Span<byte> hashBuffer = stackalloc byte[32];
            HashHelpers.TryHash256(buffer.Slice(MessageHeader.Size, header.Length), hashBuffer);
            var checksum = BitConverter.ToUInt32(hashBuffer.Slice(0, 4));
            if (header.Checksum != checksum)
            {
                // ignore messages sent with invalid checksum
                log.LogWarning("Ignoring message with incorrect checksum {expected} {actual}", checksum, header.Checksum);
                reader.AdvanceTo(buffer.GetPosition(messageLength));
                return null;
            }

            if (Message.TryRead(buffer.Slice(0, messageLength), header, out var message))
            {
                log.LogDebug("Receive {message}", message.GetType().Name);
                reader.AdvanceTo(buffer.GetPosition(messageLength));

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

        public static async ValueTask<Message> ReceiveMessage(PipeReader reader, uint magic, ILogger log, CancellationToken token = default)
        {   
            while (true)
            {
                var readResult = await reader.ReadAsync(token).ConfigureAwait(false);
                var message = ReceiveMessage(readResult, reader, magic, log, token);
                if (message != null)
                {
                    return message;
                }
                else
                {
                    continue;
                }
            }
        }

        public static ValueTask SendMessage<T>(PipeWriter writer, uint magic, string command, in T payload, ILogger log, CancellationToken token)
            where T : IWritable<T>
        {
            log.LogDebug("SendMessage {magic} {command} {payload}", magic, command, typeof(T).Name);

            var payloadSize = payload.Size;
            var messageSize = MessageHeader.Size + payloadSize;
            var messageMemory = writer.GetMemory(messageSize);

            Span<byte> payloadSpan = default;
            if (payloadSize > 0)
            {
                payloadSpan = messageMemory.Slice(MessageHeader.Size, payloadSize).Span;
                var payloadWriter = new BufferWriter<byte>(payloadSpan);
                payload.WriteTo(ref payloadWriter);
                payloadWriter.Commit();
                Debug.Assert(payloadWriter.Span.IsEmpty);
            }

            Span<byte> hashBuffer = stackalloc byte[32];
            HashHelpers.TryHash256(payloadSpan, hashBuffer);
            var checksum = BitConverter.ToUInt32(hashBuffer.Slice(0, 4));

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

            writer.Advance(messageSize);

            // https://github.com/dotnet/runtime/issues/31503#issuecomment-554415966
            var task = writer.FlushAsync(token);
            if (task.IsCompletedSuccessfully)
            {
                var _ = task.Result;
                return default;
            }

            return new ValueTask(task.AsTask());
        }

        public static ValueTask SendEmptyMessage(PipeWriter writer, uint magic, string command, ILogger log, CancellationToken token)
        {
            return SendMessage<NullPayload>(writer, magic, command, default, log, token);
        }

        private struct NullPayload : IWritable<NullPayload>
        {
            public int Size => 0;

            public void WriteTo(ref BufferWriter<byte> writer)
            {
            }
        }

        public static async Task<VersionPayload> PerformVersionHandshake(IDuplexPipe duplexPipe, uint magic, VersionPayload payload, ILogger log, CancellationToken token = default)
        {
            static async Task<T> ReceiveTypedMessage<T>(PipeReader reader, uint magic, ILogger log, CancellationToken token)
                where T : Message
            {
                var message = await ReceiveMessage(reader, magic, log, token).ConfigureAwait(false);
                if (message is T typedMessage)
                {
                    return typedMessage;
                }

                throw new InvalidOperationException($"Expected {typeof(T).Name} message, received {message.GetType().Name}");
            }

            log.LogDebug("Sending version message");
            await SendMessage<VersionPayload>(duplexPipe.Output, magic, VersionMessage.CommandText, payload, log, token).ConfigureAwait(false);

            var versionMessage = await ReceiveTypedMessage<VersionMessage>(duplexPipe.Input, magic, log, token).ConfigureAwait(false);
            log.LogDebug("Received version message {startHeight} {userAgent}", versionMessage.StartHeight, versionMessage.UserAgent);

            log.LogDebug("Sending verack message");
            await SendMessage<NullPayload>(duplexPipe.Output, magic, VerAckMessage.CommandText, default, log, token).ConfigureAwait(false);

            var verAckMessage = await ReceiveTypedMessage<VerAckMessage>(duplexPipe.Input, magic, log, token).ConfigureAwait(false);
            log.LogDebug("Received verack message");

            return versionMessage.Payload;
        }
    }
}
