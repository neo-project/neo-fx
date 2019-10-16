using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{
    [Obsolete]
    public sealed class PublishTransaction : Transaction
    {
        public ReadOnlyMemory<byte> Script;
        public ReadOnlyMemory<ContractParameterType> ParameterList;
        public ContractParameterType ReturnType;
        public bool NeedStorage;
        public string Name;
        public string CodeVersion;
        public string Author;
        public string Email;
        public string Description;

        public PublishTransaction(ReadOnlyMemory<byte> script, ReadOnlyMemory<ContractParameterType> parameterList,
                                     ContractParameterType returnType, bool needStorage, string name, string codeVersion,
                                     string author, string email, string description, byte version,
                                     ReadOnlyMemory<TransactionAttribute> attributes,
                                     ReadOnlyMemory<CoinReference> inputs, ReadOnlyMemory<TransactionOutput> outputs,
                                     ReadOnlyMemory<Witness> witnesses)
            : base(version, attributes, inputs, outputs, witnesses)
        {
            Script = script;
            ParameterList = parameterList;
            ReturnType = returnType;
            NeedStorage = needStorage;
            Name = name;
            CodeVersion = codeVersion;
            Author = author;
            Email = email;
            Description = description;
        }
    }
}
