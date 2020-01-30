using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using DevHawk.Buffers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NeoFx.P2P.Messages;

namespace NeoFx.P2P
{
    public sealed class NeoClient
    {
        static SHA256 _hash = SHA256.Create();

        private readonly IDuplexPipe duplexPipe;
        private readonly ILogger log;
        private readonly string errorLogDirectory;

        public NeoClient(IDuplexPipe duplexPipe, ILoggerFactory? loggerFactory = null, string? errorLogDirectory = null)
        {
            this.duplexPipe = duplexPipe;
            log = loggerFactory?.CreateLogger(nameof(NeoClient)) ?? NullLogger.Instance;
            this.errorLogDirectory = errorLogDirectory ?? string.Empty;
        }

        internal static uint CalculateChecksum(ReadOnlySpan<byte> source)
        {
            Span<byte> buf1 = stackalloc byte[32];
            _hash.TryComputeHash(source, buf1, out var bytesWritten);
            Span<byte> buf2 = stackalloc byte[32];
            _hash.TryComputeHash(buf1, buf2, out bytesWritten);

            return BitConverter.ToUInt32(buf2.Slice(0, 4));
        }

        private static void WriteHeader(ref BufferWriter<byte> writer, uint magic, string command, ReadOnlySpan<byte> payload)
        {
            var checksum = CalculateChecksum(payload);

            using var commandOwner = MemoryPool<byte>.Shared.Rent(MessageHeader.CommandSize);
            var commandSpan = commandOwner.Memory.Span.Slice(0, MessageHeader.CommandSize);
            commandSpan.Clear();
            System.Text.Encoding.ASCII.GetBytes(command, commandSpan);

            writer.WriteLittleEndian(magic);
            writer.Write(commandSpan);
            writer.WriteLittleEndian((uint)payload.Length);
            writer.WriteLittleEndian(checksum);
            writer.Commit();
        }

        private struct NullPayload : IPayload<NullPayload>
        {
            public int Size => 0;

            public void WriteTo(ref BufferWriter<byte> writer)
            {
            }
        }

        private ValueTask<FlushResult> SendMessage<T>(uint magic, string command, in T payload, CancellationToken token)
            where T : IPayload<T>
        {
            var payloadSize = payload.Size;
            var messageSize = MessageHeader.Size + payloadSize;
            var messageMemory = duplexPipe.Output.GetMemory(messageSize);

            Span<byte> payloadSpan = default;
            if (payloadSize > 0)
            {
                payloadSpan = messageMemory.Slice(MessageHeader.Size, payloadSize).Span;
                var payloadWriter = new BufferWriter<byte>(payloadSpan);
                payload.WriteTo(ref payloadWriter);
                Debug.Assert(payloadWriter.Span.IsEmpty);
            }

            var headerWriter = new BufferWriter<byte>(messageMemory.Slice(0, MessageHeader.Size).Span);
            WriteHeader(ref headerWriter, magic, command, payloadSpan);
            Debug.Assert(headerWriter.Span.IsEmpty);

            duplexPipe.Output.Advance(messageSize);
            return duplexPipe.Output.FlushAsync(token);
        }


        public ValueTask<FlushResult> SendVersion(uint magic, in VersionPayload payload, CancellationToken token = default)
        {
            return SendMessage<VersionPayload>(magic, VersionMessage.CommandText, payload, token);
        }

        public ValueTask<FlushResult> SendVerAck(uint magic, CancellationToken token = default)
        {
            return SendMessage<NullPayload>(magic, VerAckMessage.CommandText, default, token);
        }

        public ValueTask<FlushResult> SendGetAddr(uint magic, CancellationToken token = default)
        {
            return SendMessage<NullPayload>(magic, GetAddrMessage.CommandText, default, token);
        }

        public ValueTask<FlushResult> SendGetData(uint magic, in InventoryPayload payload, CancellationToken token = default)
        {
            return SendMessage<InventoryPayload>(magic, GetDataMessage.CommandText, payload, token);
        }

        public ValueTask<FlushResult> SendGetBlocks(uint magic, in HashListPayload payload, CancellationToken token = default)
        {
            return SendMessage<HashListPayload>(magic, GetBlocksMessage.CommandText, payload, token);
        }

        public ValueTask<FlushResult> SendGetHeaders(uint magic, in HashListPayload payload, CancellationToken token = default)
        {
            return SendMessage<HashListPayload>(magic, GetHeadersMessage.CommandText, payload, token);
        }

        public async IAsyncEnumerable<Message> GetMessages([EnumeratorCancellation] CancellationToken token = default)
        {
            var inputPipe = duplexPipe.Input;

            while (true)
            {
                var read = await inputPipe.ReadAsync(token).ConfigureAwait(false);
                log.LogDebug("read {length} bytes from pipe {IsCompleted} {IsCanceled}",
                    read.Buffer.Length, read.IsCompleted, read.IsCanceled);
                if (read.IsCompleted || read.IsCanceled || token.IsCancellationRequested)
                {
                    break;
                }

                var buffer = read.Buffer;
                if (buffer.Length < MessageHeader.Size)
                {
                    // haven't received enough data to read the message header
                    inputPipe.AdvanceTo(buffer.GetPosition(0),
                        buffer.GetPosition(buffer.Length));
                    continue;
                }

                if (!MessageHeader.TryRead(buffer, out var header))
                {
                    throw new Exception("could not parse message header");
                }
                log.LogDebug("Received {command} message header {magic} {length} {checksum}",
                    header.Command, header.Magic, header.Length, header.Checksum);

                if (buffer.Length < MessageHeader.Size + header.Length)
                {
                    // haven't received enough data to read the message payload
                    inputPipe.AdvanceTo(buffer.GetPosition(0),
                        buffer.GetPosition(buffer.Length));
                    continue;
                }

                if (Message.TryRead(buffer, header, out var message))
                {
                    message.LogMessage(log);
                    inputPipe.AdvanceTo(buffer.GetPosition(MessageHeader.Size + message.Length));
                    yield return message;
                }
                else
                {
                    if (errorLogDirectory.Length != 0)
                    {
                        var path = System.IO.Path.Combine(errorLogDirectory, "{header.Checksum}.neohawk.log");
                        using var logStream = System.IO.File.OpenWrite(path);
                        var segmentCount = 0;
                        foreach (var segment in buffer)
                        {
                            segmentCount++;
                            logStream.Write(segment.Span);
                        }
                    }
                    log.LogError("Message Parse {command} {length} {checksum}",
                        header.Command, header.Length, header.Checksum);

                    throw new Exception($"could not parse message {header.Command}");
                }
            }
        }
    }
}
