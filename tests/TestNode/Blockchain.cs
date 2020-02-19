using System.Threading.Tasks;
using NeoFx.Models;

namespace NeoFx.TestNode
{
    class Blockchain 
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
