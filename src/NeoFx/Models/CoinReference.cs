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
    }
}
