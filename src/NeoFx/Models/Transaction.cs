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

        public Transaction(TransactionType type, byte version, ReadOnlyMemory<byte> transactionData, ReadOnlyMemory<TransactionAttribute> attributes = default, ReadOnlyMemory<CoinReference> inputs = default, ReadOnlyMemory<TransactionOutput> outputs = default, ReadOnlyMemory<Witness> witnesses = default)
        {
            Type = type;
            Version = version;
            TransactionData = transactionData;
            Attributes = attributes;
            Inputs = inputs;
            Outputs = outputs;
            Witnesses = witnesses;
        }

        public static ReadOnlyMemory<byte> InvocationTxData(ReadOnlySpan<byte> script, long /*fixed8*/ gas)
        {
            var size = script.GetVarSize() + sizeof(long);

            var buffer = new byte[size];

            return buffer;
        }
    }
}
