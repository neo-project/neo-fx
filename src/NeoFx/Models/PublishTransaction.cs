using NeoFx.Storage;
using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

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

        public static bool TryRead(ref SpanReader<byte> reader, byte version, [NotNullWhen(true)] out PublishTransaction? tx)
        {
            static bool TryReadNeedStorage(ref SpanReader<byte> reader, byte version, out bool needStorage)
            {
                if (version < 1)
                {
                    needStorage = false;
                    return true;
                }

                if (reader.TryRead(out var value))
                {
                    needStorage = value != 0;
                    return true;
                }

                needStorage = default;
                return false;
            }

            if (reader.TryReadVarArray(out var script)
                && reader.TryReadVarArray(out var byteParameterList)
                && reader.TryRead(out byte returnType)
                && TryReadNeedStorage(ref reader, version, out var needStorage)
                && reader.TryReadVarString(out var name)
                && reader.TryReadVarString(out var codeVersion)
                && reader.TryReadVarString(out var author)
                && reader.TryReadVarString(out var email)
                && reader.TryReadVarString(out var description)
                && reader.TryReadVarArray<TransactionAttribute>(TransactionAttribute.TryRead, out var attributes)
                && reader.TryReadVarArray<CoinReference>(CoinReference.TryRead, out var inputs)
                && reader.TryReadVarArray<TransactionOutput>(TransactionOutput.TryRead, out var outputs)
                && reader.TryReadVarArray<Witness>(Witness.TryRead, out var witnesses))
            {
                var parameterList = Unsafe.As<ImmutableArray<byte>, ImmutableArray<ContractParameterType>> (ref byteParameterList);
                tx = new PublishTransaction(script, parameterList, (ContractParameterType)returnType, needStorage, name,
                                            codeVersion, author, email, description, version, attributes, inputs,
                                            outputs, witnesses);
                return true;
            }

            tx = null;
            return false;
        }


    }
}
