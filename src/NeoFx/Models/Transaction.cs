using System;
using System.Buffers;
using System.Buffers.Binary;
using NeoFx.Storage;

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

        public int Size => 2 + TransactionData.GetVarSize()
            + (Inputs.Length * CoinReference.Size)
            + (Outputs.Length * TransactionOutput.Size)
            + Attributes.GetVarSize(a => a.Size)
            + Witnesses.GetVarSize(w => w.Size);

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

            if (buffer.AsSpan().TryWriteVarInt(script.Length, out var scriptLengthWritten)
                && script.TryCopyTo(buffer.AsSpan().Slice(scriptLengthWritten))
                && BinaryPrimitives.TryWriteInt64LittleEndian(buffer.AsSpan().Slice(scriptLengthWritten + script.Length), gas))
            {
                return buffer;
            }

            throw new Exception();
        }
    }
}
