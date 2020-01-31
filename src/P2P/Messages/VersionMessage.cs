using System;
using System.Diagnostics.CodeAnalysis;
using DevHawk.Buffers;
using Microsoft.Extensions.Logging;

namespace NeoFx.P2P.Messages
{
    internal sealed class VersionMessage : Message
    {
        public const string CommandText = "version";

        public readonly VersionPayload Payload;

        public uint Version => Payload.Version;
        public ulong Services => Payload.Services;
        public DateTimeOffset Timestamp => Payload.Timestamp;
        public ushort Port => Payload.Port;
        public uint Nonce => Payload.Nonce;
        public string UserAgent => Payload.UserAgent;
        public uint StartHeight => Payload.StartHeight;
        public bool Relay => Payload.Relay;

        public VersionMessage(in MessageHeader header, in VersionPayload payload) : base(header)
        {
            Payload = payload;
        }

        public override void LogMessage(ILogger logger)
        {
            logger.LogInformation("Receive {messageType} {userAgent} {startHeight} {timestamp}",
                nameof(VersionMessage),
                Payload.UserAgent,
                Payload.StartHeight,
                Payload.Timestamp);
        }

        public static bool TryRead(ref BufferReader<byte> reader, in MessageHeader header, [MaybeNullWhen(false)] out VersionMessage message)
        {
            if (VersionPayload.TryRead(ref reader, out var payload))
            {
                message = new VersionMessage(header, payload);
                return true;
            }

            message = default!;
            return false;
        }
    }
}
