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
using NeoFx.Storage;

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

        public async ValueTask Flush(CancellationToken token = default)
        {
            await duplexPipe.Output.FlushAsync(token);
        }

        // private MemoryBufferWriter<byte> GetPayloadWriter(int payloadSize)
        // {
        //     var messageSize = MessageHeader.Size + payloadSize;
        //     var messageMemory = duplexPipe.Output.GetMemory(messageSize);

        //     var payloadMemory = messageMemory.Slice(MessageHeader.Size, payloadSize);
        //     return new MemoryBufferWriter<byte>(payloadMemory);
        // }

        // private ValueTask<FlushResult> SendMessage(uint magic, string command, ReadOnlySpan<byte> payload,
        //     CancellationToken token = default)
        // {
        //     var checksum = CalculateChecksum(payload);
        //     log.LogDebug("SendMessage {command} {magic} {length} {checksum}",
        //         command, magic, payload.Length, checksum);

        //     duplexPipe.Output.Write(magic);
        //     var cmdSpan = duplexPipe.Output.GetSpan(MessageHeader.CommandSize)
        //         .Slice(0, MessageHeader.CommandSize);
        //     cmdSpan.Clear();
        //     System.Text.Encoding.UTF8.GetBytes(command, cmdSpan);
        //     duplexPipe.Output.Advance(MessageHeader.CommandSize);
        //     duplexPipe.Output.Write((uint)payload.Length);
        //     duplexPipe.Output.Write(checksum);
        //     duplexPipe.Output.Advance(payload.Length);
        //     return duplexPipe.Output.FlushAsync(token);
        // }

        // public ValueTask<FlushResult> SendVersion(uint magic, in VersionPayload payload)
        // {
        //     var payloadWriter = GetPayloadWriter(payload.GetSize());
        //     payloadWriter.Write(payload);
        //     Debug.Assert(payloadWriter.FreeCapacity == 0);

        //     return SendMessage(magic, VersionMessage.CommandText, payloadWriter.WrittenSpan);
        // }

        // public ValueTask<FlushResult> SendVerAck(uint magic)
        // {
        //     return SendMessage(magic, VerAckMessage.CommandText, ReadOnlySpan<byte>.Empty);
        // }

        // public ValueTask<FlushResult> SendGetAddr(uint magic)
        // {
        //     return SendMessage(magic, GetAddrMessage.CommandText, ReadOnlySpan<byte>.Empty);
        // }

        // public ValueTask<FlushResult> SendGetData(uint magic, in InventoryPayload payload)
        // {
        //     var payloadWriter = GetPayloadWriter(payload.GetSize());
        //     payloadWriter.Write(payload);
        //     Debug.Assert(payloadWriter.FreeCapacity == 0);

        //     return SendMessage(magic, GetDataMessage.CommandText, payloadWriter.WrittenSpan);
        // }

        // public ValueTask<FlushResult> SendHashListMessage(uint magic, string commandText, in HashListPayload payload)
        // {
        //     var payloadWriter = GetPayloadWriter(payload.GetSize());
        //     payloadWriter.Write(payload);
        //     Debug.Assert(payloadWriter.FreeCapacity == 0);

        //     return SendMessage(magic, commandText, payloadWriter.WrittenSpan);
        // }

        // public ValueTask<FlushResult> SendGetBlocks(uint magic, in HashListPayload payload)
        // {
        //     return SendHashListMessage(magic, GetBlocksMessage.CommandText, payload);
        // }

        // public ValueTask<FlushResult> SendGetHeaders(uint magic, in HashListPayload payload)
        // {
        //     return SendHashListMessage(magic, GetHeadersMessage.CommandText, payload);
        // }

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
