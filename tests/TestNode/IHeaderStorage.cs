using NeoFx.Models;

namespace NeoFx.TestNode
{
    interface IHeaderStorage
    {
        int Count { get; }
        void Add(in BlockHeader header);
        bool TryGet(uint index, out BlockHeader header);
        bool TryGet(in UInt256 hash, out BlockHeader header);
        bool TryGetLastHash(out UInt256 hash);
    }
}
