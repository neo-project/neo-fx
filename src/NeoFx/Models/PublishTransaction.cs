using DevHawk.Buffers;
using NeoFx.Storage;
using System;
using System.Buffers;
using System.Collections.Generic;
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
                                  IEnumerable<TransactionAttribute>? attributes = null,
                                  IEnumerable<CoinReference>? inputs = null,
                                  IEnumerable<TransactionOutput>? outputs = null,
                                  IEnumerable<Witness>? witnesses = null)
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

        private PublishTransaction(ImmutableArray<byte> script,
                          ImmutableArray<ContractParameterType> parameterList,
                          ContractParameterType returnType,
                          bool needStorage,
                          string name,
                          string codeVersion,
                          string author,
                          string email,
                          string description,
                          byte version,
                          CommonData commonData)
            : base(version, commonData)
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

        public static bool TryRead(ref BufferReader<byte> reader, byte version, [NotNullWhen(true)] out PublishTransaction? tx)
        {
            static bool TryReadNeedStorage(ref BufferReader<byte> reader, byte version, out bool needStorage)
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
                && TryReadCommonData(ref reader, out var commonData))
            {
                var parameterList = Unsafe.As<ImmutableArray<byte>, ImmutableArray<ContractParameterType>> (ref byteParameterList);
                tx = new PublishTransaction(script, parameterList, (ContractParameterType)returnType, needStorage, name,
                                            codeVersion, author, email, description, version, commonData);
                return true;
            }

            tx = null;
            return false;
        }

        public override TransactionType GetTransactionType() => TransactionType.Publish;

        public override int GetTransactionDataSize()
        {
            return Script.GetVarSize() 
                + ParameterList.GetVarSize(1)
                + sizeof(byte) // return type
                + (Version >= 1 ? sizeof(byte) : 0) // need storage
                + Name.GetVarSize()
                + CodeVersion.GetVarSize()
                + Author.GetVarSize()
                + Email.GetVarSize()
                + Description.GetVarSize();
        }

        public override void WriteTransactionData(ref BufferWriter<byte> writer)
        {
            writer.WriteVarArray(Script);
            var byteParameterList = Unsafe.As<ImmutableArray<ContractParameterType>, byte[]>(ref ParameterList);
            writer.WriteVarArray(byteParameterList);
            writer.Write((byte)ReturnType);
            if (Version >= 1)
            {
                writer.Write(NeedStorage ? (byte)1 : (byte)0);
            }
            writer.WriteVarString(Name);
            writer.WriteVarString(CodeVersion);
            writer.WriteVarString(Author);
            writer.WriteVarString(Email);
            writer.WriteVarString(Description);
        }
    }
}
