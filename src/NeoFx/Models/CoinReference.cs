using DevHawk.Buffers;
using NeoFx.Storage;
using System.Buffers;

namespace NeoFx.Models
{
    public readonly struct CoinReference : IWritable<CoinReference>
    {
        public readonly struct Factory : IFactoryReader<CoinReference>
        {
            public bool TryReadItem(ref BufferReader<byte> reader, out CoinReference value) => TryRead(ref reader, out value);
        }
        
        public readonly UInt256 PrevHash;
        public readonly ushort PrevIndex;

        public const int Size = sizeof(ushort) + UInt256.Size;

        public CoinReference(in UInt256 prevHash, ushort prevIndex)
        {
            PrevHash = prevHash;
            PrevIndex = prevIndex;
        }

        public static bool TryRead(ref BufferReader<byte> reader, out CoinReference value)
        {
            if (UInt256.TryRead(ref reader, out var prevHash)
                && reader.TryReadLittleEndian(out ushort prevIndex))
            {
                value = new CoinReference(prevHash, prevIndex);
                return true;
            }

            value = default;
            return false;
        }

        public void WriteTo(ref BufferWriter<byte> writer)
        {
            PrevHash.WriteTo(ref writer);
            writer.WriteLittleEndian(PrevIndex);
        }
    }
}
