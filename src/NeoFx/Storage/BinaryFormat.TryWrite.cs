using NeoFx.Models;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace NeoFx.Storage
{
    public static partial class BinaryFormat
    {
        public static bool TryWrite(this StorageKey key, Span<byte> span, out int bytesWritten)
        {
            var keySize = key.GetSize();
            if (span.Length >= keySize && key.ScriptHash.TryWrite(span))
            {
                span = span.Slice(UInt160.Size);
                var keySpan = key.Key.Span;

                while (keySpan.Length >= StorageKeyBlockSize)
                {
                    keySpan.Slice(0, StorageKeyBlockSize).CopyTo(span);
                    span[StorageKeyBlockSize] = 0;

                    keySpan = keySpan.Slice(StorageKeyBlockSize);
                    span = span.Slice(StorageKeyBlockSize + 1);
                }

                Debug.Assert(span.Length == StorageKeyBlockSize + 1);

                keySpan.CopyTo(span);
                span.Slice(keySpan.Length).Clear();
                span[StorageKeyBlockSize] = (byte)(StorageKeyBlockSize - keySpan.Length);

                bytesWritten = keySize;
                return true;
            }

            bytesWritten = default;
            return false;
        }

        public static void Write(this IBufferWriter<byte> buffer, in TransactionAttribute attribute)
        {
            buffer.Write((byte)attribute.Usage);
            buffer.WriteByteArray(attribute.Data.Span);
        }

        public static void Write(this IBufferWriter<byte> buffer, in CoinReference input)
        {
            buffer.Write(input.PrevHash);
            buffer.Write(input.PrevIndex);
        }

        public static void Write(this IBufferWriter<byte> buffer, in TransactionOutput output)
        {
            buffer.Write(output.AssetId);
            buffer.Write(output.Value);
            buffer.Write(output.ScriptHash);
        }

        public static void Write(this IBufferWriter<byte> buffer, in StateDescriptor value)
        {
            buffer.Write((byte)value.Type);
            buffer.WriteVarArray(value.Key.Span);
            buffer.WriteVarString(value.Field);
            buffer.WriteVarArray(value.Value.Span);
        }

        private delegate void Writer(Span<byte> span);

        private static void Write(IBufferWriter<byte> buffer, int size, Writer writer)
        {
            var span = buffer.GetSpan(size);
            writer(span);
            buffer.Advance(size);
        }

        public static void Write(this IBufferWriter<byte> buffer, uint value) 
            => Write(buffer, sizeof(uint), span => BinaryPrimitives.WriteUInt32LittleEndian(span, value));

        public static void Write(this IBufferWriter<byte> buffer, ushort value)
            => Write(buffer, sizeof(ushort), span => BinaryPrimitives.WriteUInt16LittleEndian(span, value));

        public static void Write(this IBufferWriter<byte> buffer, byte value) 
            => Write(buffer, 1, span => span[0] = value);

        public static void Write(this IBufferWriter<byte> buffer, bool value)
            => Write(buffer, 1, span => span[0] = (byte)(value ? 1 : 0));

        public static void Write(this IBufferWriter<byte> buffer, in Fixed8 value)
            => Write(buffer, Fixed8.Size, value.Write);

        public static void Write(this IBufferWriter<byte> buffer, in UInt160 value)
            => Write(buffer, UInt160.Size, value.Write);

        public static void Write(this IBufferWriter<byte> buffer, in UInt256 value)
            => Write(buffer, UInt256.Size, value.Write);

        public static void Write(this IBufferWriter<byte> buffer, in ContractParameterType value)
            => Write(buffer, (byte)value);

        public static void Write(this IBufferWriter<byte> buffer, EncodedPublicKey value)
            => Write(buffer, value.Size, value.Write);

        public static void Write(this IBufferWriter<byte> buffer, MinerTransaction tx)
        {
            buffer.Write(tx.Nonce);
        }

        public static void Write(this IBufferWriter<byte> buffer, ClaimTransaction tx)
        {
            buffer.WriteVarArray(tx.Claims.Span, Write);
        }

#pragma warning disable CS0612 // Type or member is obsolete
        public static void Write(this IBufferWriter<byte> buffer, EnrollmentTransaction tx)
#pragma warning restore CS0612 // Type or member is obsolete
        {
            buffer.Write(tx.PublicKey);
        }

        public static void Write(this IBufferWriter<byte> buffer, RegisterTransaction tx)
        {
            buffer.Write((byte)tx.AssetType);
            Debug.Assert(tx.Name.Length <= 1024);
            buffer.WriteVarString(tx.Name); 
            buffer.Write(tx.Amount);
            buffer.Write(tx.Precision);
            buffer.Write(tx.Owner);
            buffer.Write(tx.Admin);
        }

        public static void Write(this IBufferWriter<byte> buffer, StateTransaction tx)
        {
            buffer.WriteVarArray(tx.Descriptors.Span, Write);
        }

#pragma warning disable CS0612 // Type or member is obsolete
        public static void Write(this IBufferWriter<byte> buffer, PublishTransaction tx)
#pragma warning restore CS0612 // Type or member is obsolete
        {
            buffer.WriteVarArray(tx.Script.Span);
            buffer.WriteVarArray(tx.ParameterList.Span, Write);
            buffer.Write((byte)tx.ReturnType);
            if (tx.Version >= 1)
            {
                buffer.Write(tx.NeedStorage);
            }
            buffer.WriteVarString(tx.Name);
            buffer.WriteVarString(tx.CodeVersion);
            buffer.WriteVarString(tx.Author);
            buffer.WriteVarString(tx.Email);
            buffer.WriteVarString(tx.Description);
        }

        public static void Write(this IBufferWriter<byte> buffer, InvocationTransaction tx)
        {
            Debug.Assert(tx.Script.Length <= 65536);
            buffer.WriteByteArray(tx.Script);
            buffer.Write(tx.Gas);
        }

        public static void WriteData(this IBufferWriter<byte> buffer, Transaction tx)
        {
            buffer.Write((byte)tx.GetTransactionType());
            buffer.Write(tx.Version);

            switch (tx)
            {
                case MinerTransaction miner:
                    buffer.Write(miner);
                    break;
                case ClaimTransaction claim:
                    buffer.Write(claim);
                    break;
#pragma warning disable CS0612 // Type or member is obsolete
                case EnrollmentTransaction enrollment:
#pragma warning restore CS0612 // Type or member is obsolete
                    buffer.Write(enrollment);
                    break;
                case RegisterTransaction register:
                    buffer.Write(register);
                    break;
                case StateTransaction state:
                    buffer.Write(state);
                    break;
#pragma warning disable CS0612 // Type or member is obsolete
                case PublishTransaction publish:
#pragma warning restore CS0612 // Type or member is obsolete
                    buffer.Write(publish);
                    break;
                case InvocationTransaction invocation:
                    buffer.Write(invocation);
                    break;
                case IssueTransaction _:
                case ContractTransaction _:
                    break;
                default:
                    throw new ArgumentException(nameof(tx));
            }
        }
    }
}
