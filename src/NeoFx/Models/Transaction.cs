using System;
using System.Buffers;

namespace NeoFx.Models
{
    public readonly struct Transaction
    {
        public readonly TransactionType Type;
        public readonly byte Version;
        public readonly ReadOnlyMemory<byte> TransactionData;
        public readonly ReadOnlyMemory<TransactionAttribute> Attributes;
        public readonly ReadOnlyMemory<CoinReference> Inputs;
        public readonly ReadOnlyMemory<TransactionOutput> Outputs;
        public readonly ReadOnlyMemory<Witness> Witnesses;

        public Transaction(TransactionType type, byte version, ReadOnlyMemory<byte> transactionData, ReadOnlyMemory<TransactionAttribute> attributes, ReadOnlyMemory<CoinReference> inputs, ReadOnlyMemory<TransactionOutput> outputs, ReadOnlyMemory<Witness> witnesses)
        {
            Type = type;
            Version = version;
            TransactionData = transactionData;
            Attributes = attributes;
            Inputs = inputs;
            Outputs = outputs;
            Witnesses = witnesses;
        }
    }
}
