using DevHawk.Buffers;
using Microsoft.Extensions.Logging;
using NeoFx.Models;
using System.Diagnostics.CodeAnalysis;

namespace NeoFx.P2P.Messages
{
    public sealed class BlockMessage : Message
    {
        public const string CommandText = "block";

        public readonly Block Block;

        public BlockMessage(in MessageHeader header, in Block block) : base(header)
        {
            Block = block;
        }

        public override void LogMessage(ILogger logger)
        {
            logger.LogInformation("Receive {messageType} {index}",
                nameof(BlockMessage),
                Block.Index);
        }

        public static bool TryRead(ref BufferReader<byte> reader, in MessageHeader header, [MaybeNullWhen(false)] out BlockMessage message)
        {
            if (Block.TryRead(ref reader, out var block))
            {
                message = new BlockMessage(header, block);
                return true;
            }

            message = null!;
            return false;
        }
    }
}
