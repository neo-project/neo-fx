using NeoFx.Storage;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NeoFx.Models
{
    public sealed class MinerTransaction : Transaction
    {
        public readonly uint Nonce;

        public MinerTransaction(uint nonce,
                                byte version,
                                IEnumerable<TransactionAttribute> attributes,
                                IEnumerable<CoinReference> inputs,
                                IEnumerable<TransactionOutput> outputs,
                                IEnumerable<Witness> witnesses)
            : base(version, attributes, inputs, outputs, witnesses)
        {
            Nonce = nonce;
        }

        private MinerTransaction(uint nonce, byte version, CommonData commonData)
            : base(version, commonData)
        {
            Nonce = nonce;
        }

        public static bool TryRead(ref BufferReader<byte> reader, byte version, [NotNullWhen(true)] out MinerTransaction? tx)
        {
            if (reader.TryReadLittleEndian(out uint nonce)
                && TryReadCommonData(ref reader, out var commonData))
            {
                tx = new MinerTransaction(nonce, version, commonData);
                return true;
            }

            tx = null;
            return false;
        }

        public override void WriteTransactionData(IBufferWriter<byte> writer)
        {
            writer.WriteLittleEndian((byte)TransactionType.Miner);
            writer.WriteLittleEndian(Version);
            writer.WriteLittleEndian(Nonce);
        }
    }
}
