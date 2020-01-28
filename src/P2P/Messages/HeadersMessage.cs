using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using DevHawk.Buffers;
using Microsoft.Extensions.Logging;
using NeoFx.Models;

namespace NeoFx.P2P.Messages
{
    public sealed class HeadersMessage : Message
    {
        public const string CommandText = "headers";

        public readonly HeadersPayload Payload;
        public ImmutableArray<BlockHeader> Headers => Payload.Headers;

        public HeadersMessage(in MessageHeader header, in HeadersPayload payload) : base(header)
        {
            Payload = payload;
        }

        public override void LogMessage(ILogger logger)
        {
            logger.LogInformation("Receive {messageType} {count}",
                nameof(HeadersMessage),
                Payload.Headers.Length);
        }

        public static bool TryRead(ref BufferReader<byte> reader, in MessageHeader header, [MaybeNullWhen(false)] out HeadersMessage message)
        {
            if (HeadersPayload.TryRead(ref reader, out var payload))
            {
                message = new HeadersMessage(header, payload);
                return true;
            }

            message = null!;
            return false;
        }
    }
}
