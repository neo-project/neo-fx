using System;
using System.Diagnostics.CodeAnalysis;
using DevHawk.Buffers;
using Microsoft.Extensions.Logging;

namespace NeoFx.P2P.Messages
{
    public sealed class PingMessage : Message
    {
        public const string CommandText = "ping";

        public readonly PingPongPayload Payload;

        public uint LastBlockIndex => Payload.LastBlockIndex;
        public DateTimeOffset Timestamp => Payload.Timestamp;
        public uint Nonce => Payload.Nonce;

        public PingMessage(in MessageHeader header, in PingPongPayload payload) : base(header)
        {
            Payload = payload;
        }

        public override void LogMessage(ILogger logger)
        {
            logger.LogInformation("Receive {messageType} {index} {timestamp} {nonce}",
                nameof(PingMessage),
                Payload.LastBlockIndex,
                Payload.Timestamp,
                Payload.Nonce);
        }

        public static bool TryRead(ref BufferReader<byte> reader, in MessageHeader header, [NotNullWhen(true)] out PingMessage? message)
        {
            if (PingPongPayload.TryRead(ref reader, out var payload))
            {
                message = new PingMessage(header, payload);
                return true;
            }

            message = null!;
            return false;
        }
    }
}
