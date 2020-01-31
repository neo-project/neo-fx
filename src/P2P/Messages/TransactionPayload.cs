using DevHawk.Buffers;
using NeoFx.Models;

namespace NeoFx.P2P.Messages
{
    public readonly struct TransactionPayload : IPayload<TransactionPayload>
    {
        public readonly Transaction Transaction;

        public TransactionPayload(in Transaction transaction)
        {
            Transaction = transaction;
        }

        public int Size => Transaction.Size;

        public static bool TryRead(ref BufferReader<byte> reader, out TransactionPayload payload)
        {
            if (Transaction.TryRead(ref reader, out var tx))
            {
                payload = new TransactionPayload(tx);
                return true;
            }

            payload = default!;
            return false;
        }

        public void WriteTo(ref BufferWriter<byte> writer)
        {
            Transaction.WriteTo(ref writer);
        }

    }
}
