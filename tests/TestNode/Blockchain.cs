using System.Threading.Tasks;
using NeoFx.Models;

namespace NeoFx.TestNode
{
    interface IBlockchain
    {
        Task<(uint index, UInt256 hash)> GetLastBlockHash();
        Task AddBlock(in Block block);
    }

    class Blockchain : IBlockchain
    {
        public Task AddBlock(in Block block)
        {
            throw new System.NotImplementedException();
        }

        public Task<(uint index, UInt256 hash)> GetLastBlockHash()
        {
            throw new System.NotImplementedException();
        }
    }
}
