using NeoFx.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Storage
{
    public interface IBlockchainStorage
    {
        uint Height { get; }
        public UInt256 GoverningTokenHash { get; }
        public UInt256 UtilityTokenHash { get; }
        bool TryGetBlock(in UInt256 key, out Block block);
        bool TryGetBlock(uint index, out Block block);
        bool TryGetBlock(in UInt256 key, out BlockHeader header, out ReadOnlyMemory<UInt256> hashes);
        bool TryGetBlockHash(uint index, out UInt256 hash);
        bool TryGetTransaction(in UInt256 key, out uint index, out Transaction tx);
        bool TryGetStorage(in StorageKey key, out StorageItem item);
        bool TryGetContract(in UInt160 key, out DeployedContract value);
        bool TryGetUnspentCoins(in UInt256 key, out ReadOnlyMemory<CoinState> coins);
        IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> EnumerateStorage(in UInt160 scriptHash);
    }
}
