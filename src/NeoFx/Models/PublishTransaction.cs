using System;
using System.Collections.Immutable;

namespace NeoFx.Models
{
    [Obsolete]
    public sealed class PublishTransaction : Transaction
    {
        public ImmutableArray<byte> Script;
        public ImmutableArray<ContractParameterType> ParameterList;
        public ContractParameterType ReturnType;
        public bool NeedStorage;
        public string Name;
        public string CodeVersion;
        public string Author;
        public string Email;
        public string Description;

        public PublishTransaction(ImmutableArray<byte> script,
                                  ImmutableArray<ContractParameterType> parameterList,
                                  ContractParameterType returnType,
                                  bool needStorage,
                                  string name,
                                  string codeVersion,
                                  string author,
                                  string email,
                                  string description,
                                  byte version,
                                  ImmutableArray<TransactionAttribute> attributes,
                                  ImmutableArray<CoinReference> inputs,
                                  ImmutableArray<TransactionOutput> outputs,
                                  ImmutableArray<Witness> witnesses)
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
