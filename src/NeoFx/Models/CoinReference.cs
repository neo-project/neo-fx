using NeoFx.Storage;
using System.Buffers;

namespace NeoFx.Models
{
    public readonly struct CoinReference : IWritable<CoinReference>
    {
        public readonly UInt256 PrevHash;
        public readonly ushort PrevIndex;

        public const int Size = sizeof(ushort) + UInt256.Size;

        public CoinReference(in UInt256 prevHash, ushort prevIndex)
        {
            PrevHash = prevHash;
            PrevIndex = prevIndex;
        }

        public static bool TryRead(ref SpanReader<byte> reader, out CoinReference value)
        {
            if (UInt256.TryRead(ref reader, out var prevHash)
                && reader.TryRead(out ushort prevIndex))
            {
                value = new CoinReference(prevHash, prevIndex);
                return true;
            }

            value = default;
            return false;
        }

        public void Write(IBufferWriter<byte> writer)
        {
            writer.Write(PrevHash);
            writer.WriteLittleEndian(PrevIndex);
        }
    }
}
