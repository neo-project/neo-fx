using System;
using System.Diagnostics.CodeAnalysis;
using DevHawk.Buffers;
using Microsoft.Extensions.Logging;

namespace NeoFx.P2P.Messages
{
    public sealed class PongMessage : Message
    {
        public const string CommandText = "pong";

        public readonly PingPongPayload Payload;

        public uint LastBlockIndex => Payload.LastBlockIndex;
        public DateTimeOffset Timestamp => Payload.Timestamp;
        public uint Nonce => Payload.Nonce;

        public PongMessage(in MessageHeader header, in PingPongPayload payload) : base(header)
        {
            Payload = payload;
        }

        public override void LogMessage(ILogger logger)
        {
            logger.LogInformation("Receive {messageType} {index} {timestamp} {nonce}",
                nameof(PongMessage),
                Payload.LastBlockIndex,
                Payload.Timestamp,
                Payload.Nonce);
        }

        public static bool TryRead(ref BufferReader<byte> reader, in MessageHeader header, [NotNullWhen(true)] out PongMessage? message)
        {
            if (PingPongPayload.TryRead(ref reader, out var payload))
            {
                message = new PongMessage(header, payload);
                return true;
            }

            message = null!;
            return false;
        }
    }
}
