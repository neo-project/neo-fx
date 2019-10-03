using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{
    public readonly struct Transaction
    {
        public enum TransactionType : byte
        {
            Miner = 0x00,
            Issue = 0x01,
            Claim = 0x02,
            Enrollment = 0x20,
            Register = 0x40,
            Contract = 0x80,
            State = 0x90,
            Publish = 0xd0,
            Invocation = 0xd1
        }

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

        public static bool TryRead(ref SequenceReader<byte> reader, out Transaction tx)
        {
            if (reader.TryRead(out byte type) 
                && reader.TryRead(out byte version)
                && TryReadTransactionData(ref reader, (TransactionType)type, out var data)
                && reader.TryReadVarArray<TransactionAttribute>(TransactionAttribute.TryRead, out var attributes)
                && reader.TryReadVarArray<CoinReference>(CoinReference.TryRead, out var inputs)
                && reader.TryReadVarArray<TransactionOutput>(TransactionOutput.TryRead, out var outputs)
                && reader.TryReadVarArray<Witness>(Witness.TryRead, out var witnesses))
            {
                tx = new Transaction((TransactionType)type, version, data, attributes, inputs, outputs, witnesses);
                return true;
            }

            tx = default;
            return false;
        }

        private static bool TryReadTransactionData(ref SequenceReader<byte> reader, TransactionType type, out ReadOnlyMemory<byte> value)
        {
            switch (type)
            {
                case TransactionType.Miner:
                    // public uint Nonce;
                    return reader.TryReadByteArray(sizeof(uint), out value);
                case TransactionType.Claim:
                    // public CoinReference[] Claims;
                    {
                        if (reader.TryReadVarInt(out var count))
                        {
                            return reader.TryReadByteArray((int)count * CoinReference.Size, out value);
                        }
                    }
                    break;
                case TransactionType.Invocation:
                    // public byte[] Script;
                    // public Fixed8 Gas;
                    {
                        if (reader.TryReadVarArray(out var script) && reader.TryReadByteArray(sizeof(long), out var gas))
                        {
                            var buffer = new byte[script.Length + gas.Length];
                            script.CopyTo(buffer);
                            gas.CopyTo(buffer.AsMemory().Slice(script.Length));
                            value = buffer;
                            return true;
                        }
                    }
                    break;
                // these transactions have no transaction type specific data
                case TransactionType.Contract:
                case TransactionType.Issue:
                    {
                        value = default;
                        return true;
                    }
                // State transaction is not implemented yet
                case TransactionType.State:
                    break;
                // these transactions are obsolete
                case TransactionType.Enrollment:
                case TransactionType.Register:
                case TransactionType.Publish:
                    break;
            }

            value = default;
            return false;
        }

    }
}
