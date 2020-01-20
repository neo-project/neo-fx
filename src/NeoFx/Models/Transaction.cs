using NeoFx.Storage;
using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NeoFx.Models
{
    public abstract class Transaction
    {
        public readonly byte Version;
        public readonly ImmutableArray<TransactionAttribute> Attributes;
        public readonly ImmutableArray<CoinReference> Inputs;
        public readonly ImmutableArray<TransactionOutput> Outputs;
        public readonly ImmutableArray<Witness> Witnesses;

        protected Transaction(byte version,
                              ImmutableArray<TransactionAttribute> attributes,
                              ImmutableArray<CoinReference> inputs,
                              ImmutableArray<TransactionOutput> outputs,
                              ImmutableArray<Witness> witnesses)
        {
            Version = version;
            Attributes = attributes;
            Inputs = inputs;
            Outputs = outputs;
            Witnesses = witnesses;
        }

        public static bool TryRead(ref SpanReader<byte> reader, [NotNullWhen(true)] out Transaction? tx)
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

        public abstract void WriteTransactionData(IBufferWriter<byte> writer);
    }
}
