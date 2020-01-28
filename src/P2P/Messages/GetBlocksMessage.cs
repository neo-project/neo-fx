using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using DevHawk.Buffers;
using Microsoft.Extensions.Logging;

namespace NeoFx.P2P.Messages
{
    public sealed class GetBlocksMessage : Message
    {
        public const string CommandText = "getblocks";

        public readonly HashListPayload Payload;
        public ImmutableArray<UInt256> HashStart => Payload.HashStart;
        public UInt256 HashStop => Payload.HashStop;

        public GetBlocksMessage(in MessageHeader header, in HashListPayload payload) : base(header)
        {
            Payload = payload;
        }

        public override void LogMessage(ILogger logger)
        {
            logger.LogInformation("Receive {messageType} {hashStart} {hashStop}",
                nameof(GetBlocksMessage),
                Payload.HashStart.IsEmpty ? default : Payload.HashStart[0],
                Payload.HashStop);
        }

        public static bool TryRead(ref BufferReader<byte> reader, in MessageHeader header, [MaybeNullWhen(false)] out GetBlocksMessage message)
        {
            if (HashListPayload.TryRead(ref reader, out var payload))
            {
                message = new GetBlocksMessage(header, payload);
                return true;
            }

            message = null!;
            return false;
        }
    }
}
