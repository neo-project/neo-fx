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

        public readonly Transaction Transaction;

        public TransactionMessage(in MessageHeader header, in Transaction tx) : base(header)
        {
            Transaction = tx;
        }

        public override void LogMessage(ILogger logger)
        {
            logger.LogInformation("Receive {messageType} {type}",
                nameof(TransactionMessage),
                GetTransactionType(Transaction));
        }

        private static TransactionType GetTransactionType(Transaction tx)
        {
            switch (tx)
            {
                case MinerTransaction _:
                    return TransactionType.Miner;
                case IssueTransaction _:
                    return TransactionType.Issue;
                case ContractTransaction _:
                    return TransactionType.Contract;
                case ClaimTransaction _:
                    return TransactionType.Claim;
#pragma warning disable CS0612 // Type or member is obsolete
                case EnrollmentTransaction _:
#pragma warning restore CS0612 // Type or member is obsolete
                    return TransactionType.Enrollment;
                case RegisterTransaction _:
                    return TransactionType.Register;
                case StateTransaction _:
                    return TransactionType.State;
#pragma warning disable CS0612 // Type or member is obsolete
                case PublishTransaction _:
#pragma warning restore CS0612 // Type or member is obsolete
                    return TransactionType.Publish;
                case InvocationTransaction _:
                    return TransactionType.Invocation;
            }

            throw new ArgumentException(nameof(tx));
        }

        public static bool TryRead(ref BufferReader<byte> reader, in MessageHeader header, [MaybeNullWhen(false)] out TransactionMessage message)
        {
            if (Transaction.TryRead(ref reader, out var block))
            {
                message = new TransactionMessage(header, block);
                return true;
            }

            message = null!;
            return false;
        }
    }
}
