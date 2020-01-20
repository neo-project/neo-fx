using NeoFx.Storage;

namespace NeoFx.Models
{
    public readonly struct CoinReference
    {
        public readonly UInt256 PrevHash;
        public readonly ushort PrevIndex;

        public CoinReference(in UInt256 prevHash, ushort prevIndex)
        {
            PrevHash = prevHash;
            PrevIndex = prevIndex;
        }

        public static bool TryRead(ref SpanReader<byte> reader, out CoinReference value)
        {
            if (reader.TryRead(out UInt256 prevHash)
                && reader.TryRead(out ushort prevIndex))
            {
                value = new CoinReference(prevHash, prevIndex);
                return true;
            }

            value = default;
            return false;
        }
    }
}
