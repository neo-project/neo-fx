using DevHawk.Buffers;
using Microsoft.Extensions.Logging;
using NeoFx.Models;
using System.Diagnostics.CodeAnalysis;

namespace NeoFx.P2P.Messages
{
    public sealed class BlockMessage : Message
    {
        public const string CommandText = "block";

        public readonly BlockPayload Payload;
        public Block Block => Payload.Block;

        public BlockMessage(in MessageHeader header, in BlockPayload payload) : base(header)
        {
            Payload = payload;
        }

        public override void LogMessage(ILogger logger)
        {
            logger.LogInformation("Receive {messageType} {index}",
                nameof(BlockMessage),
                Block.Index);
        }

        public static bool TryRead(ref BufferReader<byte> reader, in MessageHeader header, [MaybeNullWhen(false)] out BlockMessage message)
        {
            if (BlockPayload.TryRead(ref reader, out var payload))
            {
                message = new BlockMessage(header, payload);
                return true;
            }

            message = null!;
            return false;
        }
    }
}
