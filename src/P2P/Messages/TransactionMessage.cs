using DevHawk.Buffers;
using Microsoft.Extensions.Logging;
using NeoFx.Models;
using NeoFx.Storage;
using System;
using System.Diagnostics.CodeAnalysis;

namespace NeoFx.P2P.Messages
{
    public sealed class TransactionMessage : Message
    {
        public const string CommandText = "tx";

        public readonly TransactionPayload Payload;
        public Transaction Transaction => Payload.Transaction;

        public TransactionMessage(in MessageHeader header, in TransactionPayload payload) : base(header)
        {
            Payload = payload;
        }

        public override void LogMessage(ILogger logger)
        {
            logger.LogInformation("Receive {messageType} {type}",
                nameof(TransactionMessage),
                Transaction.GetTransactionType());
        }

        public static bool TryRead(ref BufferReader<byte> reader, in MessageHeader header, [MaybeNullWhen(false)] out TransactionMessage message)
        {
            if (TransactionPayload.TryRead(ref reader, out var payload))
            {
                message = new TransactionMessage(header, payload);
                return true;
            }

            message = null!;
            return false;
        }
    }
}
