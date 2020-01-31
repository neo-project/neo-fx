using System.Collections.Generic;
using NeoFx.Models;

namespace NeoFx.TestNode
{
    public sealed class MemoryHeaderStorage : IHeaderStorage
    {
        private readonly SortedList<uint, BlockHeader> headers = new SortedList<uint, BlockHeader>();
        private readonly Dictionary<UInt256, uint> hashIndex = new Dictionary<UInt256, uint>();

        public int Count => headers.Count;

        public void Add(in BlockHeader header)
        {
            headers.Add(header.Index, header);
            hashIndex.Add(header.CalculateHash(), header.Index);
        }

        public bool TryGet(uint index, out BlockHeader header)
        {
            return headers.TryGetValue(index, out header);
        }

        public bool TryGet(in UInt256 hash, out BlockHeader header)
        {
            if (hashIndex.TryGetValue(hash, out var index))
            {
                return headers.TryGetValue(index, out header);
            }

            header = default;
            return false;
        }

        public bool TryGetLastHash(out UInt256 hash)
        {
            if (headers.Count > 0)
            {
                for (int i = 1; i < headers.Count; i++)
                {
                    if (headers.Values[i].Index != i)
                    {
                        hash = headers.Values[i - 1].CalculateHash();
                        return true;
                    }
                }

                hash = headers.Values[headers.Count - 1].CalculateHash();
                return true;
            }

            hash = default;
            return false;
        }
    }
}
