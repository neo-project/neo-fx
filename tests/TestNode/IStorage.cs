using NeoFx.Models;

namespace NeoFx.TestNode
{
    interface IStorage
    {
        (uint index, UInt256 hash) GetLastBlockHash();
        void AddBlock(in Block block);
        (UInt256, UInt256) ProcessUnverifiedBlocks();
    }
}
