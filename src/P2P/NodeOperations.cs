using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
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
        private static uint CalculateChecksum(ReadOnlySequence<byte> buffer)
        {
            Span<byte> hashBuffer = stackalloc byte[32];
            HashHelpers.TryHash256(buffer, hashBuffer);
            return BitConverter.ToUInt32(hashBuffer.Slice(0, 4));
        }

        public static async ValueTask<Message> ReceiveMessage(PipeReader reader, uint magic, ILogger log, CancellationToken token = default)
        {
            while (true)
            {
                var readResult = await reader.ReadAsync(token).ConfigureAwait(false);
                log.LogDebug("read {length} bytes from pipe {IsCompleted} {IsCanceled}",
                    readResult.Buffer.Length, readResult.IsCompleted, readResult.IsCanceled);

                var buffer = readResult.Buffer;
                if (buffer.Length < MessageHeader.Size)
                {
                    log.LogDebug("Haven't received enough data to read the message header {bufferLength}", buffer.Length);
                    reader.AdvanceTo(buffer.Start, buffer.End);
                    continue;
                }

                if (!MessageHeader.TryRead(buffer, out var header))
                {
                    throw new InvalidDataException("MessageHeader could not be parsed");
                }
                log.LogDebug("Received {command} message header {magic} {length} {checksum}",
                    header.Command, header.Magic, header.Length, header.Checksum);

                var messageLength = MessageHeader.Size + header.Length;
                if (buffer.Length < messageLength)
                {
                    log.LogDebug("Haven't received enough data to read the message payload {bufferNeeded} {bufferLength}",
                        messageLength, buffer.Length);
                    reader.AdvanceTo(buffer.Start, buffer.End);
                    continue;
                }

                if (header.Magic != magic)
                {
                    // ignore messages sent with the wrong magic value
                    log.LogWarning("Ignoring message with incorrect magic {expected} {actual}", magic, header.Magic);
                    reader.AdvanceTo(buffer.GetPosition(messageLength));
                    continue;
                }

                var checksum =  CalculateChecksum(buffer.Slice(MessageHeader.Size, header.Length)); 
                if (header.Checksum != checksum)
                {
                    // ignore messages sent with invalid checksum
                    log.LogWarning("Ignoring message with incorrect checksum {expected} {actual}", checksum, header.Checksum);
                    reader.AdvanceTo(buffer.GetPosition(messageLength));
                    continue;
                }

                if (Message.TryRead(buffer.Slice(0, messageLength), header, out var message))
                {
                    log.LogDebug("Receive {message}", message.GetType().Name);
                    reader.AdvanceTo(buffer.GetPosition(messageLength));
                    return message;
                }
                else
                {
                    throw new InvalidDataException($"'{header.Command}' Message could not be parsed");
                }
            }
        }

        private static ValueTask SendMessage(PipeWriter writer, uint magic, string command, Span<byte> messageSpan, ILogger log, CancellationToken token)
        {
            log.LogDebug("SendMessage {magic} {command} {messageSize}", magic, command, messageSpan.Length);
            var payloadSpan = messageSpan.Slice(MessageHeader.Size);

            Span<byte> hashBuffer = stackalloc byte[32];
            HashHelpers.TryHash256(payloadSpan, hashBuffer);
            var checksum = BitConverter.ToUInt32(hashBuffer.Slice(0, 4));

            var headerWriter = new BufferWriter<byte>(messageSpan.Slice(0, MessageHeader.Size));
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

            writer.Advance(messageSpan.Length);

            // https://github.com/dotnet/runtime/issues/31503#issuecomment-554415966
            var task = writer.FlushAsync(token);
            if (task.IsCompletedSuccessfully)
            {
                var _ = task.Result;
                return default;
            }

            return new ValueTask(task.AsTask());
        }

        public static ValueTask SendMessage<T>(PipeWriter writer, uint magic, string command, in T payload, ILogger log, CancellationToken token)
            where T : IWritable<T>
        {
            log.LogDebug("SendMessage<{payload}> {magic} {command}", typeof(T).Name, magic, command);

            var payloadSize = payload.Size;
            var messageSize = MessageHeader.Size + payloadSize;
            var messageSpan = writer.GetMemory(messageSize).Slice(0, messageSize).Span;

            if (payloadSize > 0)
            {
                var payloadSpan = messageSpan.Slice(MessageHeader.Size, payloadSize);
                var payloadWriter = new BufferWriter<byte>(payloadSpan);
                payload.WriteTo(ref payloadWriter);
                payloadWriter.Commit();
                Debug.Assert(payloadWriter.Span.IsEmpty);
            }

            return SendMessage(writer, magic, command, messageSpan, log, token);
        }

        public static ValueTask SendEmptyMessage(PipeWriter writer, uint magic, string command, ILogger log, CancellationToken token)
        {
            var messageSpan = writer.GetMemory(MessageHeader.Size).Slice(0, MessageHeader.Size).Span;
            return SendMessage(writer, magic, command, messageSpan, log, token);
        }

        public static ValueTask<VersionPayload> PerformVersionHandshake(IDuplexPipe duplexPipe, uint magic, in VersionPayload payload, ILogger log, CancellationToken token = default)
        {
            log.LogDebug("Sending version message");
            var sendVersionTask = SendMessage<VersionPayload>(duplexPipe.Output, magic, VersionMessage.CommandText, payload, log, token);
            return AwaitHelper(sendVersionTask);

            // break await operations into separate helper function so VersionPayload can be an in parameter
            async ValueTask<VersionPayload> AwaitHelper(ValueTask task)
            {
                await task.ConfigureAwait(false);

                var versionMessage = await ReceiveTypedMessage<VersionMessage>().ConfigureAwait(false);
                log.LogDebug("Received version message {startHeight} {userAgent}", versionMessage.StartHeight, versionMessage.UserAgent);

                log.LogDebug("Sending verack message");
                await SendEmptyMessage(duplexPipe.Output, magic, VerAckMessage.CommandText, log, token).ConfigureAwait(false);

                var verAckMessage = await ReceiveTypedMessage<VerAckMessage>().ConfigureAwait(false);
                log.LogDebug("Received verack message");

                return versionMessage.Payload;
            }

            async ValueTask<T> ReceiveTypedMessage<T>()
                where T : Message
            {
                var message = await ReceiveMessage(duplexPipe.Input, magic, log, token).ConfigureAwait(false);
                if (message is T typedMessage)
                {
                    return typedMessage;
                }

                throw new InvalidOperationException($"Expected {typeof(T).Name} message, received {message.GetType().Name}");
            }
        }
    }
}
