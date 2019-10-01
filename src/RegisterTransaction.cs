using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{

    [Obsolete]
    public class RegisterTransaction : Transaction
    {
        public override TransactionType Type => TransactionType.RegisterTransaction;

        //public AssetType AssetType;
        public string Name;
        public Fixed8 Amount;
        public byte Precision;
        //public ECPoint Owner;
        public UInt160 Admin;

    }
}
