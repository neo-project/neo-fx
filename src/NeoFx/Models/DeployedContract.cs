using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{
    public readonly struct DeployedContract
    {
        [Flags]
        public enum PropertyState : byte
        {
            NoProperty = 0,

            HasStorage = 1 << 0,
            HasDynamicInvoke = 1 << 1,
            Payable = 1 << 2
        }

        public readonly ReadOnlyMemory<byte> Script;
        public readonly ReadOnlyMemory<ContractParameterType> ParameterList;
        public readonly ContractParameterType ReturnType;
        public readonly PropertyState ContractProperties;
        public readonly string Name;
        public readonly string CodeVersion;
        public readonly string Author;
        public readonly string Email;
        public readonly string Description;

        public bool HasStorage => (ContractProperties & PropertyState.HasStorage) != 0;
        public bool HasDynamicInvoke => (ContractProperties & PropertyState.HasDynamicInvoke) != 0;
        public bool Payable => (ContractProperties & PropertyState.Payable) != 0;

        //private readonly Lazy<UInt160> scriptHash;

        //public UInt160 ScriptHash => scriptHash.Value;

        //private static UInt160 CalculateScriptHash(ReadOnlyMemory<byte> script)
        //{
        //    Span<byte> buffer = stackalloc byte[HashHelpers.Hash160Size];
        //    if (HashHelpers.TryHash160(script.Span, buffer))
        //    {
        //        return new UInt160(buffer);
        //    }

        //    throw new Exception();
        //}

        public DeployedContract(ReadOnlyMemory<byte> script, ReadOnlyMemory<ContractParameterType> parameterList, ContractParameterType returnType, PropertyState contractProperties, string name, string codeVersion, string author, string email, string description)
        {
            Script = script;
            ParameterList = parameterList;
            ReturnType = returnType;
            ContractProperties = contractProperties;
            Name = name;
            CodeVersion = codeVersion;
            Author = author;
            Email = email;
            Description = description;

            //scriptHash = new Lazy<UInt160>(() => CalculateScriptHash(script));
        }
    }
}
