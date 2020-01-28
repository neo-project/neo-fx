using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using DevHawk.Buffers;
using Microsoft.Extensions.Logging;

namespace NeoFx.P2P.Messages
{
    public sealed class GetDataMessage : Message
    {
        public const string CommandText = "getdata";

        public readonly InventoryPayload Payload;

        public InventoryPayload.InventoryType Type => Payload.Type;
        public ImmutableArray<UInt256> Hashes => Payload.Hashes;

        public GetDataMessage(in MessageHeader header, in InventoryPayload payload) : base(header)
        {
            Payload = payload;
        }

        public override void LogMessage(ILogger logger)
        {
            logger.LogInformation("Receive {messageType} {type} {count}",
                nameof(GetDataMessage),
                Payload.Type,
                Payload.Hashes.Length);
        }

        public static bool TryRead(ref BufferReader<byte> reader, in MessageHeader header, [MaybeNullWhen(false)] out GetDataMessage message)
        {
            if (InventoryPayload.TryRead(ref reader, out var payload))
            {
                message = new GetDataMessage(header, payload);
                return true;
            }

            message = null!;
            return false;
        }
    }
}
