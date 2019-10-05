using NeoFx.Models;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Storage
{
    public static class BinaryReader
    {
        public static bool TryRead(ref this SequenceReader<byte> reader, out short value) =>
            reader.TryRead(sizeof(short), BinaryPrimitives.TryReadInt16LittleEndian, out value);

        public static bool TryRead(ref this SequenceReader<byte> reader, out int value) =>
            reader.TryRead(sizeof(int), BinaryPrimitives.TryReadInt32LittleEndian, out value);

        public static bool TryRead(ref this SequenceReader<byte> reader, out long value) =>
            reader.TryRead(sizeof(long), BinaryPrimitives.TryReadInt64LittleEndian, out value);

        public static bool TryRead(ref this SequenceReader<byte> reader, out ushort value) =>
            reader.TryRead(sizeof(ushort), BinaryPrimitives.TryReadUInt16LittleEndian, out value);

        public static bool TryRead(ref this SequenceReader<byte> reader, out uint value) =>
            reader.TryRead(sizeof(uint), BinaryPrimitives.TryReadUInt32LittleEndian, out value);

        public static bool TryRead(ref this SequenceReader<byte> reader, out ulong value) =>
            reader.TryRead(sizeof(ulong), BinaryPrimitives.TryReadUInt64LittleEndian, out value);

        public static bool TryReadBigEndian(ref this SequenceReader<byte> reader, out short value) =>
            reader.TryRead(sizeof(short), BinaryPrimitives.TryReadInt16BigEndian, out value);

        public static bool TryReadBigEndian(ref this SequenceReader<byte> reader, out int value) =>
            reader.TryRead(sizeof(int), BinaryPrimitives.TryReadInt32BigEndian, out value);

        public static bool TryReadBigEndian(ref this SequenceReader<byte> reader, out long value) =>
            reader.TryRead(sizeof(long), BinaryPrimitives.TryReadInt64BigEndian, out value);

        public static bool TryReadBigEndian(ref this SequenceReader<byte> reader, out ushort value) =>
            reader.TryRead(sizeof(ushort), BinaryPrimitives.TryReadUInt16BigEndian, out value);

        public static bool TryReadBigEndian(ref this SequenceReader<byte> reader, out uint value) =>
            reader.TryRead(sizeof(uint), BinaryPrimitives.TryReadUInt32BigEndian, out value);

        public static bool TryReadBigEndian(ref this SequenceReader<byte> reader, out ulong value) =>
            reader.TryRead(sizeof(ulong), BinaryPrimitives.TryReadUInt64BigEndian, out value);

        public static bool TryRead(ref this SequenceReader<byte> reader, out UInt160 value) =>
            reader.TryRead(UInt160.Size, UInt160.TryRead, out value);

        public static bool TryRead(ref this SequenceReader<byte> reader, out UInt256 value) =>
            reader.TryRead(UInt256.Size, UInt256.TryRead, out value);

        public static bool TryRead(ref this SequenceReader<byte> reader, out Witness value)
        {
            if (reader.TryReadVarArray(out ReadOnlyMemory<byte> invocationScript)
                && reader.TryReadVarArray(out ReadOnlyMemory<byte> verificationScript))
            {
                value = new Witness(invocationScript, verificationScript);
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryRead(ref this SequenceReader<byte> reader, out BlockHeader value)
        {
            if (reader.TryRead(out uint version)
                && reader.TryRead(out UInt256 prevHash)
                && reader.TryRead(out UInt256 merkleRoot)
                && reader.TryRead(out uint timestamp)
                && reader.TryRead(out uint index)
                && reader.TryRead(out ulong consensusData)
                && reader.TryRead(out UInt160 nextConsensus)
                && reader.TryRead(out byte witnessCount) && witnessCount == 1
                && reader.TryRead(out Witness witness))
            {
                value = new BlockHeader(version, prevHash, merkleRoot, timestamp, index, consensusData, nextConsensus, witness);
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryRead(ref SequenceReader<byte> reader, out TransactionAttribute value)
        {
            static bool TryReadAttributeData(ref SequenceReader<byte> reader, TransactionAttribute.UsageType usage, out ReadOnlyMemory<byte> value)
            {
                switch (usage)
                {
                    case TransactionAttribute.UsageType.ContractHash:
                    case TransactionAttribute.UsageType.Vote:
                    case TransactionAttribute.UsageType.ECDH02:
                    case TransactionAttribute.UsageType.ECDH03:
                    case var _ when usage >= TransactionAttribute.UsageType.Hash1 && usage <= TransactionAttribute.UsageType.Hash15:
                        return reader.TryReadByteArray(32, out value);
                    case TransactionAttribute.UsageType.Script:
                        return reader.TryReadByteArray(20, out value);
                    case TransactionAttribute.UsageType.Description:
                    case var _ when usage >= TransactionAttribute.UsageType.Remark:
                        return reader.TryReadVarArray(out value); // max == 65535
                    case TransactionAttribute.UsageType.DescriptionUrl:
                        {
                            if (reader.TryRead(out byte length)
                                && reader.TryReadByteArray(length, out var data))
                            {
                                value = data;
                                return true;
                            }
                        }
                        break;
                }

                value = default;
                return false;
            }

            if (reader.TryRead(out byte usage)
                && TryReadAttributeData(ref reader, (TransactionAttribute.UsageType)usage, out var data))
            {
                value = new TransactionAttribute((TransactionAttribute.UsageType)usage, data);
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryRead(ref this SequenceReader<byte> reader, out CoinReference value)
        {
            if (reader.TryRead(out UInt256 prevHash)
                && reader.TryRead(out ushort prevIndex))
            {
                value = new CoinReference(prevHash, prevIndex);
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryRead(ref this SequenceReader<byte> reader, out TransactionOutput value)
        {
            if (reader.TryRead(out UInt256 assetId)
               && reader.TryRead(out long outputValue)
               && reader.TryRead(out UInt160 scriptHash))
            {
                value = new TransactionOutput(assetId, outputValue, scriptHash);
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryRead(ref this SequenceReader<byte> reader, out StateDescriptor descriptor)
        {
            if (reader.TryRead(out var type)
                && reader.TryReadVarArray(out var key)
                && reader.TryReadVarString(out var field)
                && reader.TryReadVarArray(out var value))
            {
                descriptor = new StateDescriptor((StateDescriptor.StateType)type, key, field, value);
                return true;
            }

            descriptor = default;
            return false;
        }
        
        public static bool TryRead(ref this SequenceReader<byte> reader, out RegisterTransactionData data)
        {
            if (reader.TryRead(out byte assetType)
                && reader.TryReadVarString(out var name)
                && reader.TryRead(out long amount)
                && reader.TryRead(out byte precision)
                && reader.TryRead(out byte owner) && owner == 0
                && reader.TryRead(out UInt160 admin))
            {
                data = new RegisterTransactionData((AssetType)assetType, name, amount, precision, owner, admin);
                return true;
            }

            data = default;
            return false;
        }

        public static bool TryRead(ref this SequenceReader<byte> reader, out Transaction tx)
        {
            if (reader.TryRead(out byte type)
                && reader.TryRead(out byte version)
                && TryGetTransactionDataSize(reader, (TransactionType)type, out int dataSize)
                && reader.TryReadByteArray(dataSize, out var data)
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

        // note, reader parameter here is purposefully *NOT* ref. TryGetTransactionDataSize needs a 
        //       copy it can modify if it needs to
        private static bool TryGetTransactionDataSize(SequenceReader<byte> reader, TransactionType type, out int size)
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
                        if (reader.TryReadVarArray<StateDescriptor>(Storage.BinaryReader.TryRead, out var array))
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

    }
}
