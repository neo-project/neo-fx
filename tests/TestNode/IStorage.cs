using NeoFx.Models;

namespace NeoFx.TestNode
{
    interface IStorage
    {
        (uint index, UInt256 hash) GetLastBlockHash();
        void AddBlock(in Block block);
        (uint index, UInt256 hash) GetLastHeaderHash();
        void AddHeader(in BlockHeader header);

        (UInt256, UInt256) ProcessUnverifiedBlocks();
    }
}
