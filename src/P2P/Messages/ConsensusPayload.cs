using DevHawk.Buffers;
using NeoFx.Models;
using NeoFx.Storage;
using System;
using System.Collections.Immutable;

namespace NeoFx.P2P.Messages
{
    public readonly struct ConsensusPayload
    {
        public readonly uint Version;
        public readonly UInt256 PrevHash;
        public readonly uint BlockIndex;
        public readonly ushort ValidatorIndex;
        public readonly DateTimeOffset Timestamp;
        public readonly ImmutableArray<byte> Data;
        public readonly Witness Witness;

        public ConsensusPayload(uint version, UInt256 prevHash, uint blockIndex,
            ushort validatorIndex, DateTimeOffset timestamp, ImmutableArray<byte> data, in Witness witness)
        {
            Version = version;
            PrevHash = prevHash;
            BlockIndex = blockIndex;
            ValidatorIndex = validatorIndex;
            Timestamp = timestamp;
            Data = data;
            Witness = witness;
        }


        public static bool TryRead(ref BufferReader<byte> reader, out ConsensusPayload payload)
        {
            if (reader.TryReadLittleEndian(out uint version)
                && UInt256.TryRead(ref reader, out var prevHash)
                && reader.TryReadLittleEndian(out uint blockIndex)
                && reader.TryReadLittleEndian(out ushort validatorIndex)
                && reader.TryReadLittleEndian(out uint timestamp)
                && reader.TryReadVarArray(out var data)
                && Witness.TryRead(ref reader, out var witness))
            {
                payload = new ConsensusPayload(
                    version,
                    prevHash,
                    blockIndex,
                    validatorIndex,
                    DateTimeOffset.FromUnixTimeSeconds(timestamp),
                    data,
                    witness);
                return true;
            }

            payload = default;
            return false;
        }
    }
}
