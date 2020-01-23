using System;
using System.Collections.Immutable;

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

        public readonly ImmutableArray<byte> Script;
        public readonly ImmutableArray<ContractParameterType> ParameterList;
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

        //private static UInt160 CalculateScriptHash(ImmutableArray<byte> script)
        //{
        //    Span<byte> buffer = stackalloc byte[HashHelpers.Hash160Size];
        //    if (HashHelpers.TryHash160(script.Span, buffer))
        //    {
        //        return new UInt160(buffer);
        //    }

        //    throw new Exception();
        //}

        public DeployedContract(ImmutableArray<byte> script, ImmutableArray<ContractParameterType> parameterList, ContractParameterType returnType, PropertyState contractProperties, string name, string codeVersion, string author, string email, string description)
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

        //public static bool TryRead(ref this SpanReader<byte> reader, out DeployedContract value)
        //{
        //    if (reader.TryReadVarArray(out var script)
        //        && reader.TryReadVarArray(out var parameterTypes)
        //        && reader.TryRead(out byte returnType)
        //        && reader.TryRead(out byte propertyState)
        //        && reader.TryReadVarString(out var name)
        //        && reader.TryReadVarString(out var version)
        //        && reader.TryReadVarString(out var author)
        //        && reader.TryReadVarString(out var email)
        //        && reader.TryReadVarString(out var description))
        //    {
        //        value = new DeployedContract(
        //            script,
        //            ConvertContractParameterTypeMemory(parameterTypes),
        //            (ContractParameterType)returnType,
        //            (DeployedContract.PropertyState)propertyState,
        //            name,
        //            version,
        //            author,
        //            email,
        //            description);
        //        return true;
        //    }

        //    value = default;
        //    return false;
        //}

    }
}
