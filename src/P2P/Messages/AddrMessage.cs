using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using DevHawk.Buffers;
using Microsoft.Extensions.Logging;

namespace NeoFx.P2P.Messages
{
    public sealed class AddrMessage : Message
    {
        public const string CommandText = "addr";

        public readonly AddrPayload Payload;

        public ImmutableArray<NodeAddress> Addresses => Payload.Addresses;

        public AddrMessage(in MessageHeader header, in AddrPayload payload) : base(header)
        {
            Payload = payload;
        }

        public override void LogMessage(ILogger logger)
        {
            logger.LogInformation("Receive {messageType} {addressCount}",
                nameof(AddrMessage),
                Addresses.Length);
        }

        public static bool TryRead(ref BufferReader<byte> reader, in MessageHeader header, [NotNullWhen(true)] out AddrMessage? message)
        {
            if (AddrPayload.TryRead(ref reader, out var payload))
            {
                message = new AddrMessage(header, payload);
                return true;
            }

            message = default!;
            return false;
        }
    }
}
