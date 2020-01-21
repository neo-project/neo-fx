using NeoFx.Storage;
using System.Buffers;

namespace NeoFx.Models
{
    public readonly struct CoinReference : IFactoryReader<CoinReference>, IWritable<CoinReference>
    {
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

        bool IFactoryReader<CoinReference>.TryReadItem(ref BufferReader<byte> reader, out CoinReference value) => TryRead(ref reader, out value);

        public void Write(IBufferWriter<byte> writer)
        {
            writer.Write(PrevHash);
            writer.WriteLittleEndian(PrevIndex);
        }
    }
}
