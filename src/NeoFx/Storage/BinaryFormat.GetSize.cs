using NeoFx.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Storage
{
    public static partial class BinaryFormat
    {
        public const int CoinReferenceSize = sizeof(ushort) + UInt256.Size;
        public const int TransactionOutputSize = sizeof(long) + UInt256.Size + UInt160.Size;
        public static int GetSize(this TransactionAttribute attribute)
            => attribute.Data.GetVarSize() + 1;

        public static int GetSize(this CoinReference _)
            => CoinReferenceSize;

        public static int GetSize(this TransactionOutput _)
            => TransactionOutputSize;

        public static int GetSize(this Witness witness)
            => witness.InvocationScript.GetVarSize() + witness.VerificationScript.GetVarSize();

        public static int GetSize(this Transaction tx)
            => 2 + tx.TransactionData.GetVarSize()
            + tx.Inputs.GetVarSize(CoinReferenceSize)
            + tx.Outputs.GetVarSize(TransactionOutputSize)
            + tx.Attributes.GetVarSize(a => a.GetSize())
            + tx.Witnesses.GetVarSize(w => w.GetSize());
    }
}
