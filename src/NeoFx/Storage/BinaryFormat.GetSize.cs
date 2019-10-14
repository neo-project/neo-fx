using NeoFx.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace NeoFx.Storage
{
    public static partial class BinaryFormat
    {
        public const int CoinReferenceSize = sizeof(ushort) + UInt256.Size;
        public const int TransactionOutputSize = sizeof(long) + UInt256.Size + UInt160.Size;
        public const int StorageKeyBlockSize = 16;

        public static int GetSize(this StorageKey key)
            => UInt160.Size + (((key.Key.Length / StorageKeyBlockSize) + 1) * (StorageKeyBlockSize + 1));

        public static int GetSize(this TransactionAttribute attribute)
            => attribute.Data.GetVarSize() + 1;

        public static int GetSize(this Witness witness)
            => witness.InvocationScript.GetVarSize() + witness.VerificationScript.GetVarSize();

        public static int GetSize(this StateDescriptor descriptor)
            => sizeof(StateDescriptor.StateType) + descriptor.Key.GetVarSize() + descriptor.Field.GetVarSize() + descriptor.Value.GetVarSize();

        public static int GetSize(this EncodedPublicKey publicKey)
        {
            if (publicKey.Key.Length >= 1)
            {
                switch (publicKey.Key.Span[0])
                {
                    case 0x00:
                        return 1;
                    case 0x02:
                    case 0x03:
                        return 33;
                    case 0x04:
                    case 0x06:
                    case 0x07:
                        return 65;
                }
            }

            throw new ArgumentException(nameof(publicKey));
        }

        public static int GetTransactionDataSize(this Transaction tx)
        {
            switch (tx)
            {
                case MinerTransaction _:
                    return sizeof(uint);
                case IssueTransaction _:
                case ContractTransaction _:
                    return 0;
                case ClaimTransaction claim:
                    return claim.Claims.GetVarSize(CoinReferenceSize);
#pragma warning disable CS0612 // Type or member is obsolete
                case EnrollmentTransaction enrollment:
#pragma warning restore CS0612 // Type or member is obsolete
                    return enrollment.PublicKey.GetSize();
                case RegisterTransaction register:
                    return register.Name.GetVarSize()
                           + register.Owner.GetSize()
                           + sizeof(AssetType)
                           + Fixed8.Size
                           + sizeof(byte)
                           + UInt160.Size;
                case StateTransaction state:
                    return state.Descriptors.GetVarSize(d => d.GetSize());
#pragma warning disable CS0612 // Type or member is obsolete
                case PublishTransaction publish:
#pragma warning restore CS0612 // Type or member is obsolete
                    return publish.Script.GetVarSize()
                           + publish.ParameterList.GetVarSize(1)
                           + 2
                           + publish.Name.GetVarSize()
                           + publish.CodeVersion.GetVarSize()
                           + publish.Author.GetVarSize()
                           + publish.Email.GetVarSize()
                           + publish.Description.GetVarSize();
                case InvocationTransaction invocation:
                    return invocation.Script.GetVarSize() + (invocation.Version >= 1 ? Fixed8.Size : 0);
            }
            return 0;
        }

        public static int GetSize(this Transaction tx)
            => 2 + tx.GetTransactionDataSize()
            + tx.Inputs.GetVarSize(CoinReferenceSize)
            + tx.Outputs.GetVarSize(TransactionOutputSize)
            + tx.Attributes.GetVarSize(a => a.GetSize())
            + tx.Witnesses.GetVarSize(w => w.GetSize());
    }
}
