using NeoFx.Storage;
using System.Buffers;
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
                                     ImmutableArray<TransactionAttribute> attributes,
                                     ImmutableArray<CoinReference> inputs,
                                     ImmutableArray<TransactionOutput> outputs,
                                     ImmutableArray<Witness> witnesses)
            : base(version, attributes, inputs, outputs, witnesses)
        {
            Script = script;
            Gas = gas;
        }

        public static bool TryRead(ref SpanReader<byte> reader, byte version, [NotNullWhen(true)] out InvocationTransaction? tx)
        {
            static bool TryReadGas(ref SpanReader<byte> reader, byte version, out Fixed8 gas)
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
                && reader.TryReadVarArray<TransactionAttribute>(TransactionAttribute.TryRead, out var attributes)
                && reader.TryReadVarArray<CoinReference>(CoinReference.TryRead, out var inputs)
                && reader.TryReadVarArray<TransactionOutput>(TransactionOutput.TryRead, out var outputs)
                && reader.TryReadVarArray<Witness>(Witness.TryRead, out var witnesses))
            {
                tx = new InvocationTransaction(script, gas, version, attributes, inputs, outputs, witnesses);
                return true;
            }

            tx = null;
            return false;
        }

        public override void WriteTransactionData(IBufferWriter<byte> writer)
        {
            Debug.Assert(Script.Length <= 65536);
            writer.WriteLittleEndian((byte)TransactionType.Invocation);
            writer.WriteLittleEndian(Version);
            writer.WriteVarArray(Script.AsSpan());
            writer.Write(Gas);
        }
    }
}
