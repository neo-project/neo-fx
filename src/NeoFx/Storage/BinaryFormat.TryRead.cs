using NeoFx.Models;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace NeoFx.Storage
{
    public static partial class BinaryFormat
    {
        public static bool TryReadBytes(ReadOnlySpan<byte> span, out StorageKey value)
        {
            // StorageKey.Key uses an atypical storage pattern relative to other models in NEO.
            // The byte array is written in blocks of 16 bytes followed by a byte indicating how many
            // bytes of the previous block were padding. Only the last block of 16 is allowed to have
            // padding. Read blocks of 16 (plus 1 padding indication byte) until padding indication byte
            // is greater than zero.

            if (UInt160.TryRead(span, out var scriptHash))
            {
                span = span.Slice(UInt160.Size);
                var blockCount = span.Length / (StorageKeyBlockSize + 1);

                Debug.Assert((span.Length % (StorageKeyBlockSize + 1)) == 0);
                Debug.Assert(blockCount > 0);

                var padding = span[span.Length - 1];
                var bufferSize = (blockCount * StorageKeyBlockSize) - padding;
                var buffer = new byte[bufferSize];

                for (int i = 0; i < blockCount; i++)
                {
                    var src = span.Slice(
                        i * (StorageKeyBlockSize + 1),
                        StorageKeyBlockSize - ((i == blockCount - 1) ? padding : 0));
                    var dst = buffer.AsSpan().Slice(i * StorageKeyBlockSize);
                    src.CopyTo(dst);
                }

                value = new StorageKey(scriptHash, buffer);
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryRead(ref this SpanReader<byte> reader, out short value) =>
            reader.TryRead(sizeof(short), BinaryPrimitives.TryReadInt16LittleEndian, out value);

        public static bool TryRead(ref this SpanReader<byte> reader, out int value) =>
            reader.TryRead(sizeof(int), BinaryPrimitives.TryReadInt32LittleEndian, out value);

        public static bool TryRead(ref this SpanReader<byte> reader, out long value) =>
            reader.TryRead(sizeof(long), BinaryPrimitives.TryReadInt64LittleEndian, out value);

        public static bool TryRead(ref this SpanReader<byte> reader, out ushort value) =>
            reader.TryRead(sizeof(ushort), BinaryPrimitives.TryReadUInt16LittleEndian, out value);

        public static bool TryRead(ref this SpanReader<byte> reader, out uint value) =>
            reader.TryRead(sizeof(uint), BinaryPrimitives.TryReadUInt32LittleEndian, out value);

        public static bool TryRead(ref this SpanReader<byte> reader, out ulong value) =>
            reader.TryRead(sizeof(ulong), BinaryPrimitives.TryReadUInt64LittleEndian, out value);

        public static bool TryRead(ref this SpanReader<byte> reader, out UInt160 value) =>
            reader.TryRead(UInt160.Size, UInt160.TryRead, out value);

        public static bool TryRead(ref this SpanReader<byte> reader, out UInt256 value) =>
            reader.TryRead(UInt256.Size, UInt256.TryRead, out value);

        public static bool TryRead(ref this SpanReader<byte> reader, out Fixed8 value) =>
            reader.TryRead(Fixed8.Size, Fixed8.TryRead, out value);

        public static bool TryRead(ref this SpanReader<byte> reader, out EncodedPublicKey value)
        {
            static bool TryGetBufferLength(byte type, out int length)
            {
                length = type switch
                {
                    0x00 => 1,
                    var x when (0x02 <= x && x <= 0x03) => 33,
                    var x when (0x04 <= x && x <= 0x06) => 65,
                    _ => 0
                };

                return length > 0;
            }

            if (reader.TryPeek(out var type)
                && TryGetBufferLength(type, out var length)
                && reader.TryReadByteArray(length, out var key))
            {
                value = new EncodedPublicKey(key);
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryRead(ref this SpanReader<byte> reader, out Witness value)
        {
            if (reader.TryReadVarArray(65536, out var invocation)
                && reader.TryReadVarArray(65536, out var verification))
            {
                value = new Witness(invocation, verification);
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryRead(ref this SpanReader<byte> reader, out BlockHeader value)
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

        private static bool TryReadAttributeData(ref SpanReader<byte> reader, TransactionAttribute.UsageType usage, [NotNullWhen(true)] out byte[]? value)
        {
            switch (usage)
            {
                case TransactionAttribute.UsageType.ContractHash:
                case TransactionAttribute.UsageType.Vote:
                case TransactionAttribute.UsageType.ECDH02:
                case TransactionAttribute.UsageType.ECDH03:
                case var _ when usage >= TransactionAttribute.UsageType.Hash1 && usage <= TransactionAttribute.UsageType.Hash15:
                    {
                        if (reader.TryReadByteArray(32, out var buffer))
                        {
                            value = buffer;
                            return true;
                        }
                    }
                    break;
                case TransactionAttribute.UsageType.Script:
                    {
                        if (reader.TryReadByteArray(20, out var buffer))
                        {
                            value = buffer;
                            return true;
                        }
                    }
                    break;
                case TransactionAttribute.UsageType.Description:
                case var _ when usage >= TransactionAttribute.UsageType.Remark:
                    return reader.TryReadVarArray(ushort.MaxValue, out value);
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

        public static bool TryRead(ref SpanReader<byte> reader, out TransactionAttribute value)
        {
            if (reader.TryRead(out byte usage)
                && TryReadAttributeData(ref reader, (TransactionAttribute.UsageType)usage, out var data))
            {
                value = new TransactionAttribute((TransactionAttribute.UsageType)usage, data);
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryRead(ref this SpanReader<byte> reader, out CoinReference value)
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

        public static bool TryRead(ref this SpanReader<byte> reader, out TransactionOutput value)
        {
            if (reader.TryRead(out UInt256 assetId)
               && reader.TryRead(out Fixed8 outputValue)
               && reader.TryRead(out UInt160 scriptHash))
            {
                value = new TransactionOutput(assetId, outputValue, scriptHash);
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryRead(ref this SpanReader<byte> reader, out StateDescriptor descriptor)
        {
            if (reader.TryRead(out var type)
                && reader.TryReadVarArray(100, out var key)
                && reader.TryReadVarString(32, out var field)
                && reader.TryReadVarArray(65535, out var value))
            {
                descriptor = new StateDescriptor((StateDescriptor.StateType)type, key, field, value);
                return true;
            }

            descriptor = default;
            return false;
        }

        public static bool TryRead(ref this SpanReader<byte> reader, byte version, [NotNullWhen(true)] out MinerTransaction? tx)
        {
            if (reader.TryRead(out uint nonce)
                && reader.TryReadVarArray<TransactionAttribute>(TryRead, out var attributes)
                && reader.TryReadVarArray<CoinReference>(TryRead, out var inputs)
                && reader.TryReadVarArray<TransactionOutput>(TryRead, out var outputs)
                && reader.TryReadVarArray<Witness>(TryRead, out var witnesses))
            {
                tx = new MinerTransaction(nonce, version, attributes, inputs, outputs, witnesses);
                return true;
            }

            tx = null;
            return false;
        }

        public static bool TryRead(ref this SpanReader<byte> reader, byte version, [NotNullWhen(true)] out IssueTransaction? tx)
        {
            if (reader.TryReadVarArray<TransactionAttribute>(TryRead, out var attributes)
                && reader.TryReadVarArray<CoinReference>(TryRead, out var inputs)
                && reader.TryReadVarArray<TransactionOutput>(TryRead, out var outputs)
                && reader.TryReadVarArray<Witness>(TryRead, out var witnesses))
            {
                tx = new IssueTransaction(version, attributes, inputs, outputs, witnesses);
                return true;
            }

            tx = null;
            return false;
        }

        public static bool TryRead(ref this SpanReader<byte> reader, byte version, [NotNullWhen(true)] out ClaimTransaction? tx)
        {
            if (reader.TryReadVarArray<CoinReference>(TryRead, out var claims)
                && reader.TryReadVarArray<TransactionAttribute>(TryRead, out var attributes)
                && reader.TryReadVarArray<CoinReference>(TryRead, out var inputs)
                && reader.TryReadVarArray<TransactionOutput>(TryRead, out var outputs)
                && reader.TryReadVarArray<Witness>(TryRead, out var witnesses))
            {
                tx = new ClaimTransaction(claims, version, attributes, inputs, outputs, witnesses);
                return true;
            }

            tx = null;
            return false;
        }

        public static bool TryRead(ref this SpanReader<byte> reader, byte version, [NotNullWhen(true)] out RegisterTransaction? tx)
        {
            if (reader.TryRead(out byte assetType)
                && reader.TryReadVarString(1024, out var name)
                && reader.TryRead(out Fixed8 amount)
                && reader.TryRead(out byte precision)
                && reader.TryRead(out EncodedPublicKey owner)
                && reader.TryRead(out UInt160 admin)
                && reader.TryReadVarArray<TransactionAttribute>(TryRead, out var attributes)
                && reader.TryReadVarArray<CoinReference>(TryRead, out var inputs)
                && reader.TryReadVarArray<TransactionOutput>(TryRead, out var outputs)
                && reader.TryReadVarArray<Witness>(TryRead, out var witnesses))
            {
                tx = new RegisterTransaction((AssetType)assetType, name, amount, precision, owner, admin, version,
                                             attributes, inputs, outputs, witnesses);
                return true;
            }

            tx = null;
            return false;
        }

        public static bool TryRead(ref this SpanReader<byte> reader, byte version, [NotNullWhen(true)] out ContractTransaction? tx)
        {
            if (reader.TryReadVarArray<TransactionAttribute>(TryRead, out var attributes)
                && reader.TryReadVarArray<CoinReference>(TryRead, out var inputs)
                && reader.TryReadVarArray<TransactionOutput>(TryRead, out var outputs)
                && reader.TryReadVarArray<Witness>(TryRead, out var witnesses))
            {
                tx = new ContractTransaction(version, attributes, inputs, outputs, witnesses);
                return true;
            }

            tx = null;
            return false;
        }

        public static bool TryRead(ref this SpanReader<byte> reader, byte version, [NotNullWhen(true)] out InvocationTransaction? tx)
        {
            if (reader.TryReadVarArray(65536, out var script)
                && reader.TryRead(out Fixed8 gas)
                && reader.TryReadVarArray<TransactionAttribute>(TryRead, out var attributes)
                && reader.TryReadVarArray<CoinReference>(TryRead, out var inputs)
                && reader.TryReadVarArray<TransactionOutput>(TryRead, out var outputs)
                && reader.TryReadVarArray<Witness>(TryRead, out var witnesses))
            {
                tx = new InvocationTransaction(script, gas, version, attributes, inputs, outputs, witnesses);
                return true;
            }

            tx = null;
            return false;
        }

        [Obsolete]
        public static bool TryRead(ref this SpanReader<byte> reader, byte version, [NotNullWhen(true)] out EnrollmentTransaction? tx)
        {
            if (reader.TryRead(out EncodedPublicKey publicKey)
                && reader.TryReadVarArray<TransactionAttribute>(TryRead, out var attributes)
                && reader.TryReadVarArray<CoinReference>(TryRead, out var inputs)
                && reader.TryReadVarArray<TransactionOutput>(TryRead, out var outputs)
                && reader.TryReadVarArray<Witness>(TryRead, out var witnesses))
            {
                tx = new EnrollmentTransaction(publicKey, version, attributes, inputs, outputs, witnesses);
                return true;
            }

            tx = null;
            return false;
        }

        public static bool TryRead(ref this SpanReader<byte> reader, byte version, [NotNullWhen(true)] out StateTransaction? tx)
        {
            if (reader.TryReadVarArray<StateDescriptor>(TryRead, out var descriptors)
                && reader.TryReadVarArray<TransactionAttribute>(TryRead, out var attributes)
                && reader.TryReadVarArray<CoinReference>(TryRead, out var inputs)
                && reader.TryReadVarArray<TransactionOutput>(TryRead, out var outputs)
                && reader.TryReadVarArray<Witness>(TryRead, out var witnesses))
            {
                tx = new StateTransaction(descriptors, version, attributes, inputs, outputs, witnesses);
                return true;
            }

            tx = null;
            return false;
        }

        [Obsolete]
        public static bool TryRead(ref this SpanReader<byte> reader, byte version, [NotNullWhen(true)] out PublishTransaction? tx)
        {
            static bool TryReadNeedStorage(ref SpanReader<byte> reader, byte version, out bool needStorage)
            {
                if (version < 1)
                {
                    needStorage = false;
                    return true;
                }

                if (reader.TryRead(out var value))
                {
                    needStorage = value != 0;
                    return true;
                }

                needStorage = default;
                return false;
            }

            if (reader.TryReadVarArray(out var script)
                && reader.TryReadVarArray(out var parameterList)
                && reader.TryRead(out byte returnType)
                && TryReadNeedStorage(ref reader, version, out var needStorage)
                && reader.TryReadVarString(out var name)
                && reader.TryReadVarString(out var codeVersion)
                && reader.TryReadVarString(out var author)
                && reader.TryReadVarString(out var email)
                && reader.TryReadVarString(out var description)
                && reader.TryReadVarArray<TransactionAttribute>(TryRead, out var attributes)
                && reader.TryReadVarArray<CoinReference>(TryRead, out var inputs)
                && reader.TryReadVarArray<TransactionOutput>(TryRead, out var outputs)
                && reader.TryReadVarArray<Witness>(TryRead, out var witnesses))
            {

                tx = new PublishTransaction(script, ConvertContractParameterTypeMemory(parameterList),
                                            (ContractParameterType)returnType, needStorage, name, codeVersion, author,
                                            email, description, version, attributes, inputs, outputs, witnesses);
                return true;
            }

            tx = null;
            return false;
        }

        public static bool TryRead(ref this SpanReader<byte> reader, [NotNullWhen(true)] out Transaction? tx)
        {
            if (reader.TryRead(out byte type)
                && reader.TryRead(out byte version))
            {
                switch ((TransactionType)type)
                {
                    case TransactionType.Miner:
                        {
                            if (reader.TryRead(version, out MinerTransaction? _tx))
                            {
                                tx = _tx;
                                return true;
                            }
                        }
                        break;
                    case TransactionType.Issue:
                        {
                            if (reader.TryRead(version, out IssueTransaction? _tx))
                            {
                                tx = _tx;
                                return true;
                            }
                        }
                        break;
                    case TransactionType.Claim:
                        {
                            if (reader.TryRead(version, out ClaimTransaction? _tx))
                            {
                                tx = _tx;
                                return true;
                            }
                        }
                        break;
                    case TransactionType.Register:
                        {
                            if (reader.TryRead(version, out RegisterTransaction? _tx))
                            {
                                tx = _tx;
                                return true;
                            }
                        }
                        break;
                    case TransactionType.Contract:
                        {
                            if (reader.TryRead(version, out ContractTransaction? _tx))
                            {
                                tx = _tx;
                                return true;
                            }
                        }
                        break;
                    case TransactionType.Invocation:
                        {
                            if (reader.TryRead(version, out InvocationTransaction? _tx))
                            {
                                tx = _tx;
                                return true;
                            }
                        }
                        break;
                    case TransactionType.State:
                        {
                            if (reader.TryRead(version, out StateTransaction? _tx))
                            {
                                tx = _tx;
                                return true;
                            }
                        }
                        break;
                    case TransactionType.Enrollment:
                        {
#pragma warning disable CS0612 // Type or member is obsolete
                            if (reader.TryRead(version, out EnrollmentTransaction? _tx))
#pragma warning restore CS0612 // Type or member is obsolete
                            {
                                tx = _tx;
                                return true;
                            }
                        }
                        break;
                    case TransactionType.Publish:
                        {
#pragma warning disable CS0612 // Type or member is obsolete
                            if (reader.TryRead(version, out PublishTransaction? _tx))
#pragma warning restore CS0612 // Type or member is obsolete
                            {
                                tx = _tx;
                                return true;
                            }
                        }
                        break;
                }
            }

            tx = null;
            return false;
        }

        public static bool TryRead(ref this SpanReader<byte> reader, out StorageItem value)
        {
            if (reader.TryReadVarArray(out var _value)
                && reader.TryRead(out var isConstant))
            {
                value = new StorageItem(_value, isConstant != 0);
                return true;
            }

            value = default;
            return false;
        }

        // since ContractParameterType is a byte param, it's safe to cast the byte array to an 
        // array of ContractParameterType using Unsafe.As
        private static ContractParameterType[] ConvertContractParameterTypeMemory(byte[] parameterList)
            => System.Runtime.CompilerServices.Unsafe.As<byte[], ContractParameterType[]>(ref parameterList);

        public static bool TryRead(ref this SpanReader<byte> reader, out DeployedContract value)
        {
            if (reader.TryReadVarArray(out var script)
                && reader.TryReadVarArray(out var parameterTypes)
                && reader.TryRead(out byte returnType)
                && reader.TryRead(out byte propertyState)
                && reader.TryReadVarString(out var name)
                && reader.TryReadVarString(out var version)
                && reader.TryReadVarString(out var author)
                && reader.TryReadVarString(out var email)
                && reader.TryReadVarString(out var description))
            {
                value = new DeployedContract(
                    script,
                    ConvertContractParameterTypeMemory(parameterTypes),
                    (ContractParameterType)returnType,
                    (DeployedContract.PropertyState)propertyState,
                    name,
                    version,
                    author,
                    email,
                    description);
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryRead(ref this SpanReader<byte> reader, out CoinState value)
        {
            if (reader.TryRead(out byte coinState))
            {
                value = (CoinState)coinState;
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryRead(ref this SpanReader<byte> reader, out Validator value)
        {
            if (reader.TryRead(out EncodedPublicKey publicKey)
                && reader.TryRead(out byte registered)
                && reader.TryRead(out Fixed8 votes))
            {
                value = new Validator(publicKey, registered != 0, votes);
                return true;
            }
            value = default;
            return false;
        }

        public static bool TryRead(ref this SpanReader<byte> reader, out Account value)
        {
            if (reader.TryRead(out UInt160 scriptHash)
                && reader.TryRead(out byte isFrozen)
                && reader.TryReadVarArray<EncodedPublicKey>(TryRead, out var votes)
                && reader.TryReadVarInt(out var balancesCount))
            {
                Debug.Assert(balancesCount < int.MaxValue);

                var builder = ImmutableDictionary.CreateBuilder<UInt256, Fixed8>();
                for (var i = 0; i < (int)balancesCount; i++)
                {
                    if (reader.TryRead(out UInt256 assetId)
                        && reader.TryRead(out Fixed8 amount))
                    {
                        builder.Add(assetId, amount);
                    }
                    else
                    {
                        value = default;
                        return false;
                    }
                }

                value = new Account(scriptHash, isFrozen != 0, votes, builder.ToImmutable());
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryRead(ref this SpanReader<byte> reader, out Asset value)
        {
            if (reader.TryRead(out UInt256 assetId)
                && reader.TryRead(out byte assetType)
                && reader.TryReadVarString(out var name)
                && reader.TryRead(out Fixed8 amount)
                && reader.TryRead(out Fixed8 available)
                && reader.TryRead(out byte precision)
                && reader.TryRead(out byte _) // feeMode
                && reader.TryRead(out Fixed8 fee)
                && reader.TryRead(out UInt160 feeAddress)
                && reader.TryRead(out EncodedPublicKey owner)
                && reader.TryRead(out UInt160 admin)
                && reader.TryRead(out UInt160 issuer)
                && reader.TryRead(out uint expiration)
                && reader.TryRead(out byte isFrozen))
            {
                value = new Asset(
                    assetId: assetId,
                    assetType: (AssetType)assetType,
                    name: name,
                    amount: amount,
                    available: available,
                    precision: precision,
                    fee: fee,
                    feeAddress: feeAddress,
                    owner: owner,
                    admin: admin,
                    issuer: issuer,
                    expiration: expiration,
                    isFrozen: isFrozen != 0);
                return true;
            }

            value = default;
            return false;
        }
    }
}
