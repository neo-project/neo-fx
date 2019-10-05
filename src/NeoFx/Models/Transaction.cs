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

        public static bool TryRead(ref SequenceReader<byte> reader, out Transaction tx)
        {
            if (reader.TryRead(out byte type)
                && reader.TryRead(out byte version)
                && TryReadTransactionData(ref reader, (TransactionType)type, out var data)
                && reader.TryReadVarArray<TransactionAttribute>(Storage.BinaryReader.TryRead, out var attributes)
                && reader.TryReadVarArray<CoinReference>(Storage.BinaryReader.TryRead, out var inputs)
                && reader.TryReadVarArray<TransactionOutput>(Storage.BinaryReader.TryRead, out var outputs)
                && reader.TryReadVarArray<Witness>(Storage.BinaryReader.TryRead, out var witnesses))
            {
                tx = new Transaction((TransactionType)type, version, data, attributes, inputs, outputs, witnesses);
                return true;
            }

            tx = default;
            return false;
        }

        private static bool TryReadTransactionData(ref SequenceReader<byte> reader, TransactionType type, out ReadOnlyMemory<byte> value)
        {
            // note, reader parameter here is *NOT* ref so TryGetTransactionDataSize can modify it as it wishes 
            //       without affecting the "real" reader
            static bool TryGetTransactionDataSize(SequenceReader<byte> reader, TransactionType type, out int size)
            {
                switch (type)
                {
                    case TransactionType.Miner:
                        // public uint Nonce;
                        size = sizeof(uint);
                        return true;
                    case TransactionType.Claim:
                        // public CoinReference[] Claims;
                        {
                            if (reader.TryReadVarInt(out var count))
                            {
                                size = ((int)count * CoinReference.Size) + Utility.GetVarSize(count);
                                return true;
                            }
                        }
                        break;
                    case TransactionType.Invocation:
                        // public byte[] Script;
                        // public Fixed8 Gas;
                        {
                            if (reader.TryReadVarInt(out var scriptSize))
                            {
                                size = (int)scriptSize + Utility.GetVarSize(scriptSize) + sizeof(long);
                                return true;
                            }
                        }
                        break;
                    case TransactionType.Register:
                        //public AssetType AssetType;
                        //public string Name;
                        //public Fixed8 Amount;
                        //public byte Precision;
                        //public ECPoint Owner;
                        //public UInt160 Admin;
                        {
                            reader.Advance(1); // assetType
                            if (reader.TryReadVarString(out var name))
                            {
                                reader.Advance(sizeof(long) + 1); // amount + precision
                                if (reader.TryRead(out var ecPointType) && ecPointType == 0)
                                {
                                    size = 3 // assetType, precision, Owner
                                        + sizeof(long) // amount
                                        + UInt160.Size // admin
                                        + name.GetVarSize();
                                    return true;
                                }
                            }
                        }
                        break;
                    case TransactionType.State:
                        // public StateDescriptor[] Descriptors;
                        {
                            if (reader.TryReadVarArray<StateDescriptor>(StateDescriptor.TryRead, out var array))
                            {
                                size = 0;
                                for (int i = 0; i < array.Length; i++)
                                {
                                    size += array.Span[i].Size;
                                }

                                return true;
                            }
                        }
                        break;
                    // these transactions have no transaction type specific data
                    case TransactionType.Contract:
                    case TransactionType.Issue:
                        size = 0;
                        return true;
                    // these transactions are obsolete so haven't been implemented yet
                    case TransactionType.Enrollment:
                    case TransactionType.Publish:
                        break;
                }

                size = default;
                return false;
            }

            if (TryGetTransactionDataSize(reader, type, out var count))
            {
                return reader.TryReadByteArray(count, out value);
            }

            value = default;
            return false;
        }
    }
}
