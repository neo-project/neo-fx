using DevHawk.Buffers;
using NeoFx.Storage;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace NeoFx.Models
{
    public sealed class InvocationTransaction : Transaction
    {
        public readonly ImmutableArray<byte> Script;
        public readonly Fixed8 Gas;

        public InvocationTransaction(ImmutableArray<byte> script,
                                     Fixed8 gas,
                                     byte version,
                                     IEnumerable<TransactionAttribute>? attributes = null,
                                     IEnumerable<CoinReference>? inputs = null,
                                     IEnumerable<TransactionOutput>? outputs = null,
                                     IEnumerable<Witness>? witnesses = null)
            : base(version, attributes, inputs, outputs, witnesses)
        {
            Script = script == default ? ImmutableArray.Create<byte>() : script;
            Gas = gas;
        }

        private InvocationTransaction(ImmutableArray<byte> script,
                                     Fixed8 gas,
                                     byte version,
                                     CommonData commonData)
            : base(version, commonData)
        {
            Script = script == default ? ImmutableArray.Create<byte>() : script;
            Gas = gas;
        }


        public static bool TryRead(ref BufferReader<byte> reader, byte version, [NotNullWhen(true)] out InvocationTransaction? tx)
        {
            static bool TryReadGas(ref BufferReader<byte> reader, byte version, out Fixed8 gas)
            {
                if (version >= 1)
                {
                    return Fixed8.TryRead(ref reader, out gas);
                }

                gas = Fixed8.Zero;
                return true;
            }

            if (reader.TryReadVarArray(65536, out var script)
                && TryReadGas(ref reader, version, out var gas)
                && TryReadCommonData(ref reader, out var commonData))
            {
                tx = new InvocationTransaction(script, gas, version, commonData);
                return true;
            }

            tx = null;
            return false;
        }

        public override TransactionType GetTransactionType() => TransactionType.Invocation;

        public override int GetTransactionDataSize()
        {
            return Script.GetVarSize() + ((Version >= 1) ? Fixed8.Size : 0);
        }

        public override void WriteTransactionData(ref BufferWriter<byte> writer)
        {
            Debug.Assert(Script.Length <= 65536);
            writer.WriteVarArray(Script);
            if (Version >= 1)
            {
                Gas.WriteTo(ref writer);
            }
        }
    }
}
