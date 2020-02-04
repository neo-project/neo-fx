using DevHawk.Buffers;
using Microsoft.Extensions.Logging;
using NeoFx.Models;
using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NeoFx.P2P.Messages
{
    public sealed class ConsensusMessage : Message
    {
        public const string CommandText = "consensus";

        public readonly ConsensusPayload Payload;

        public uint Version => Payload.Version;
        public UInt256 PrevHash => Payload.PrevHash;
        public uint BlockIndex => Payload.BlockIndex;
        public ushort ValidatorIndex => Payload.ValidatorIndex;
        public DateTimeOffset Timestamp => Payload.Timestamp;
        public ImmutableArray<byte> Data => Payload.Data;
        public Witness Witness => Payload.Witness;

        public ConsensusMessage(in MessageHeader header, in ConsensusPayload payload)
            : base(header)
        {
            Payload = payload;
        }

        public override void LogMessage(ILogger logger)
        {
            logger.LogInformation("Receive {messageType} {blockIndex} {validatorIndex}",
                nameof(ConsensusMessage), Payload.BlockIndex, Payload.ValidatorIndex);
        }

        public static bool TryRead(ref BufferReader<byte> reader, in MessageHeader header, [NotNullWhen(true)] out ConsensusMessage? message)
        {
            if (ConsensusPayload.TryRead(ref reader, out var payload))
            {
                message = new ConsensusMessage(header, payload);
                return true;
            }

            message = null!;
            return false;
        }
    }
}
