using DevHawk.Buffers;
using NeoFx.Storage;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NeoFx.Models
{
    public sealed class ContractTransaction : Transaction
    {
        public ContractTransaction(byte version,
                                   IEnumerable<TransactionAttribute> attributes,
                                   IEnumerable<CoinReference> inputs,
                                   IEnumerable<TransactionOutput> outputs,
                                   IEnumerable<Witness> witnesses)
            : base(version, attributes, inputs, outputs, witnesses)
        {
        }

        private ContractTransaction(byte version, in CommonData commonData)
            : base(version, commonData)
        {
        }

        public static bool TryRead(ref BufferReader<byte> reader, byte version, [NotNullWhen(true)] out ContractTransaction? tx)
        {
            if (TryReadCommonData(ref reader, out var commonData))
            {
                tx = new ContractTransaction(version, commonData);
                return true;
            }

            tx = null;
            return false;
        }

        public override TransactionType GetTransactionType() => TransactionType.Contract;

        public override int GetTransactionDataSize() => 0;

        public override void WriteTransactionData(ref BufferWriter<byte> writer)
        {
        }
    }
}
