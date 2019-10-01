using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{

    [Obsolete]
    public class PublishTransaction : Transaction
    {
        public override TransactionType Type => TransactionType.PublishTransaction;

        public byte[] Script;
        //public ContractParameterType[] ParameterList;
        //public ContractParameterType ReturnType;
        public bool NeedStorage;
        public string Name;
        public string CodeVersion;
        public string Author;
        public string Email;
        public string Description;
    }
}
