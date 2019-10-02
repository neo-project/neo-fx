using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{

    [Obsolete]
    public class PublishTransaction : Transaction
    {
        public override TransactionType Type => TransactionType.PublishTransaction;

        public byte[] Script = Array.Empty<byte>();
        //public ContractParameterType[] ParameterList;
        //public ContractParameterType ReturnType;
        public bool NeedStorage;
        public string Name = string.Empty;
        public string CodeVersion = string.Empty;
        public string Author = string.Empty;
        public string Email = string.Empty;
        public string Description = string.Empty;
    }
}
