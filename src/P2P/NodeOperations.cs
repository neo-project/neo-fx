using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Net;
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
        public static async ValueTask<Message?> ReceiveMessage(PipeReader reader, EndPoint address, uint magic, ILogger log, CancellationToken token = default)
        {
            while (true)
            {
                var readResult = await reader.ReadAsync(token).ConfigureAwait(false);
                var buffer = readResult.Buffer;
                log.LogDebug("read {length} bytes from pipe {address} {IsCompleted} {IsCanceled} ",
                    readResult.Buffer.Length, address, readResult.IsCompleted, readResult.IsCanceled);
                SequencePosition consumed = buffer.Start;
                SequencePosition examined = buffer.End;

                try
                {
                    if (readResult.IsCanceled)
                    {
                        throw new OperationCanceledException();
                    }

                    var messageRead = TryReadMessage(ref buffer, out var message, out var advance);

                    if (advance)
                    {
                        consumed = buffer.End;
                        examined = consumed;
                    }

                    if (messageRead)
                    {
                        Debug.Assert(message != null);
                        return message;
                    }

                    if (readResult.IsCompleted)
                    {
                        if (buffer.Length > 0)
                        {
                            throw new InvalidDataException("Incomplete message.");
                        }

                        return null;
                    }
                }
                finally
                {
                    reader.AdvanceTo(consumed, examined);
                }
            }

            bool TryReadMessage(ref ReadOnlySequence<byte> _buffer, out Message? _message, out bool _advance)
            {
                _message = null;
                _advance = false;

                if (_buffer.Length < MessageHeader.Size)
                {
                    log.LogDebug("Haven't received enough data to read the message header {bufferLength} {address}", _buffer.Length, address);
                    return false;
                }

                if (!MessageHeader.TryRead(_buffer, out var header))
                {
                    throw new InvalidDataException("MessageHeader could not be parsed");
                }
                log.LogDebug("Received {command} message header {magic} {length} {checksum} {address}",
                    header.Command, header.Magic, header.Length, header.Checksum, address);

                var messageLength = MessageHeader.Size + header.Length;
                if (_buffer.Length < messageLength)
                {
                    log.LogDebug("Haven't received enough data to read the message payload {bufferNeeded} {bufferLength} {address}",
                        messageLength, _buffer.Length, address);
                    return false;
                }

                _buffer = _buffer.Slice(0, messageLength);
                _advance = true;
                if (header.Magic != magic)
                {
                    // ignore messages sent with the wrong magic value
                    log.LogWarning("Ignoring message with incorrect magic {expected} {actual} {address}", magic, header.Magic, address);
                    return false;
                }

                Span<byte> hashBuffer = stackalloc byte[UInt256.Size];
                HashHelpers.TryHash256(_buffer.Slice(MessageHeader.Size), hashBuffer);
                var checksum = BinaryPrimitives.ReadUInt32LittleEndian(hashBuffer.Slice(0, sizeof(uint)));
                if (header.Checksum != checksum)
                {
                    // ignore messages sent with invalid checksum
                    log.LogWarning("Ignoring message with incorrect checksum {expected} {actual} {address}", checksum, header.Checksum, address);
                    return false;
                }

                if (Message.TryRead(_buffer, header, out _message))
                {
                    log.LogDebug("Receive {message} {address}", _message.GetType().Name, address);
                    return true;
                }
                else
                {
                    throw new InvalidDataException($"'{header.Command}' Message could not be parsed");
                }
            }
        }

        public static ValueTask<FlushResult> SendMessage<T>(PipeWriter writer, uint magic, string command, in T payload, ILogger log, CancellationToken token)
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

        public static ValueTask<FlushResult> SendMessage(PipeWriter writer, uint magic, string command, ILogger log, CancellationToken token)
        {
            var messageSpan = writer.GetMemory(MessageHeader.Size).Slice(0, MessageHeader.Size).Span;
            return SendMessage(writer, magic, command, messageSpan, log, token);
        }

        private static ValueTask<FlushResult> SendMessage(PipeWriter writer, uint magic, string command, Span<byte> messageSpan, ILogger log, CancellationToken token)
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
            return writer.FlushAsync(token);
        }

        public static Task<VersionPayload> PerformVersionHandshake(IDuplexPipe duplexPipe, EndPoint address, uint magic, in VersionPayload payload, ILogger log, CancellationToken token = default)
        {
            log.LogDebug("Sending version message");
            var sendVersionTask = SendMessage<VersionPayload>(duplexPipe.Output, magic, VersionMessage.CommandText, payload, log, token);
            return AwaitHelper(sendVersionTask);

            // break await operations into separate helper function so VersionPayload can be an in parameter
            async Task<VersionPayload> AwaitHelper(ValueTask<FlushResult> task)
            {
                await task.ConfigureAwait(false);

                var versionMessage = await ReceiveTypedMessage<VersionMessage>().ConfigureAwait(false);
                log.LogDebug("Received version message {startHeight} {userAgent}", versionMessage.StartHeight, versionMessage.UserAgent);

                log.LogDebug("Sending verack message");
                await SendMessage(duplexPipe.Output, magic, VerAckMessage.CommandText, log, token).ConfigureAwait(false);

                var verAckMessage = await ReceiveTypedMessage<VerAckMessage>().ConfigureAwait(false);
                log.LogDebug("Received verack message");

                return versionMessage.Payload;
            }

            async Task<T> ReceiveTypedMessage<T>()
                where T : Message
            {
                var message = await ReceiveMessage(duplexPipe.Input, address, magic, log, token).ConfigureAwait(false);
                if (message is T typedMessage)
                {
                    return typedMessage;
                }

                var name = message == null ? "name" : message.GetType().Name;
                throw new InvalidOperationException($"Expected {typeof(T).Name} message, received {name}");
            }
        }
    }
}
