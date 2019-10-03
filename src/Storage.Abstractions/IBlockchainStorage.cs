using NeoFx.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Storage.Abstractions
{
    public interface IBlockchainStorage
    {
        uint Height { get; }
        bool TryGetBlock(in UInt256 key, out Block block);
        bool TryGetBlock(uint index, out Block block);
        bool TryGetTransaction(in UInt256 key, out uint index, out Transaction tx);
        bool TryGetStorage(in UInt160 scriptHash, ReadOnlyMemory<byte> key, out StorageItem item);
        IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> EnumerateStorage(in UInt160 scriptHash);
    }
}
