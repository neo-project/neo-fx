using DevHawk.Buffers;
using NeoFx.Storage;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NeoFx.Models
{
    public sealed class StateTransaction : Transaction
    {
        public readonly ImmutableArray<StateDescriptor> Descriptors;

        public StateTransaction(ImmutableArray<StateDescriptor> descriptors,
                                byte version,
                                IEnumerable<TransactionAttribute>? attributes = null,
                                IEnumerable<CoinReference>? inputs = null,
                                IEnumerable<TransactionOutput>? outputs = null,
                                IEnumerable<Witness>? witnesses = null)
            : base(version, attributes, inputs, outputs, witnesses)
        {
            Descriptors = descriptors;
        }

        private StateTransaction(ImmutableArray<StateDescriptor> descriptors,
                        byte version, CommonData commonData)
            : base(version, commonData)
        {
            Descriptors = descriptors == default ? ImmutableArray.Create<StateDescriptor>() : descriptors;
        }

        public static bool TryRead(ref BufferReader<byte> reader, byte version, [NotNullWhen(true)] out StateTransaction? tx)
        {
            if (reader.TryReadVarArray<StateDescriptor, StateDescriptor.Factory>(out var descriptors)
                && TryReadCommonData(ref reader, out var commonData))
            {
                tx = new StateTransaction(descriptors, version, commonData);
                return true;
            }

            tx = null;
            return false;
        }

        public override TransactionType GetTransactionType() => TransactionType.State;

        public override int GetTransactionDataSize()
        {
            return Descriptors.GetVarSize(d => d.Size);
        }

        public override void WriteTransactionData(ref BufferWriter<byte> writer)
        {
            writer.WriteVarArray(Descriptors);
        }
    }
}
