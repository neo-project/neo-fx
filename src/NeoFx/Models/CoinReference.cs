using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{
    public readonly struct CoinReference
    {
        public readonly UInt256 PrevHash;
        public readonly ushort PrevIndex;

        public const int Size = sizeof(ushort) + UInt256.Size;

        public CoinReference(in UInt256 prevHash, ushort prevIndex)
        {
            PrevHash = prevHash;
            PrevIndex = prevIndex;
        }
        public static bool TryRead(ref SequenceReader<byte> reader, out CoinReference value)
        {
            if (reader.TryReadUInt256(out var prevHash)
                && reader.TryReadUInt16LittleEndian(out ushort prevIndex))
            {
                value = new CoinReference(prevHash, prevIndex);
                return true;
            }

            value = default;
            return false;
        }
    }
}
