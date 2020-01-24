using DevHawk.Buffers;
using NeoFx.Storage;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NeoFx.Models
{
    public abstract class Transaction
    {
        public readonly struct Factory : IFactoryReader<Transaction>
        {
            public bool TryReadItem(ref BufferReader<byte> reader, [MaybeNullWhen(false)] out Transaction value)
            {
                return TryRead(ref reader, out value!);
            }
        }

        protected readonly struct CommonData
        {
            public readonly ImmutableArray<TransactionAttribute> Attributes;
            public readonly ImmutableArray<CoinReference> Inputs;
            public readonly ImmutableArray<TransactionOutput> Outputs;
            public readonly ImmutableArray<Witness> Witnesses;

            public CommonData(
                      ImmutableArray<TransactionAttribute> attributes,
                      ImmutableArray<CoinReference> inputs,
                      ImmutableArray<TransactionOutput> outputs,
                      ImmutableArray<Witness> witnesses)

            {
                Attributes = attributes;
                Inputs = inputs;
                Outputs = outputs;
                Witnesses = witnesses;
            }
        }

        public readonly byte Version;
        public readonly ImmutableArray<TransactionAttribute> Attributes;
        public readonly ImmutableArray<CoinReference> Inputs;
        public readonly ImmutableArray<TransactionOutput> Outputs;
        public readonly ImmutableArray<Witness> Witnesses;

        protected Transaction(byte version, in CommonData commonData)
        {
            Version = version;
            Attributes = commonData.Attributes;
            Inputs = commonData.Inputs;
            Outputs = commonData.Outputs;
            Witnesses = commonData.Witnesses;
        }

        protected Transaction(byte version, IEnumerable<TransactionAttribute> attributes, IEnumerable<CoinReference> inputs, IEnumerable<TransactionOutput> outputs, IEnumerable<Witness> witnesses)
        {
            static ImmutableArray<T> ToImmutableArray<T>(in IEnumerable<T> enumerable)
            {
                if (enumerable is ImmutableArray<T> immutableArray)
                {
                    return immutableArray;
                }

                if (enumerable is T[] array)
                {
                    return ImmutableArray.Create(array);
                }

                return ImmutableArray.CreateRange(enumerable);
            }

            Version = version;
            Attributes = ToImmutableArray(attributes);
            Inputs = ToImmutableArray(inputs);
            Outputs = ToImmutableArray(outputs);
            Witnesses = ToImmutableArray(witnesses);
        }

        public static bool TryRead(ref BufferReader<byte> reader, [NotNullWhen(true)] out Transaction? tx)
        {
            if (reader.TryRead(out byte type)
                && reader.TryRead(out byte version))
            {
                switch ((TransactionType)type)
                {
                    case TransactionType.Miner:
                        {
                            if (MinerTransaction.TryRead(ref reader, version, out var _tx))
                            {
                                tx = _tx;
                                return true;
                            }
                        }
                        break;
                    case TransactionType.Issue:
                        {
                            if (IssueTransaction.TryRead(ref reader, version, out var _tx))
                            {
                                tx = _tx;
                                return true;
                            }
                        }
                        break;
                    case TransactionType.Claim:
                        {
                            if (ClaimTransaction.TryRead(ref reader, version, out var _tx))
                            {
                                tx = _tx;
                                return true;
                            }
                        }
                        break;
                    case TransactionType.Register:
                        {
                            if (RegisterTransaction.TryRead(ref reader, version, out var _tx))
                            {
                                tx = _tx;
                                return true;
                            }
                        }
                        break;
                    case TransactionType.Contract:
                        {
                            if (ContractTransaction.TryRead(ref reader, version, out var _tx))
                            {
                                tx = _tx;
                                return true;
                            }
                        }
                        break;
                    case TransactionType.Invocation:
                        {
                            if (InvocationTransaction.TryRead(ref reader, version, out var _tx))
                            {
                                tx = _tx;
                                return true;
                            }
                        }
                        break;
                    case TransactionType.State:
                        {
                            if (StateTransaction.TryRead(ref reader, version, out var _tx))
                            {
                                tx = _tx;
                                return true;
                            }
                        }
                        break;
                    case TransactionType.Enrollment:
                        {
#pragma warning disable CS0612 // Type or member is obsolete
                            if (EnrollmentTransaction.TryRead(ref reader, version, out var _tx))
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
                            if (PublishTransaction.TryRead(ref reader, version, out var _tx))
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

        protected static bool TryReadCommonData(ref BufferReader<byte> reader, out CommonData commonData)
        {
            if (reader.TryReadVarArray<TransactionAttribute, TransactionAttribute.Factory>(out var attributes)
                && reader.TryReadVarArray<CoinReference, CoinReference.Factory>(out var inputs)
                && reader.TryReadVarArray<TransactionOutput, TransactionOutput.Factory>(out var outputs)
                && reader.TryReadVarArray<Witness, Witness.Factory>(out var witnesses))
            {
                commonData = new CommonData(attributes, inputs, outputs, witnesses);
                return true;
            }

            commonData = default;
            return false;
        }

        public abstract int GetTransactionDataSize();
        public abstract void WriteTransactionData(ref BufferWriter<byte> writer);
    }
}
