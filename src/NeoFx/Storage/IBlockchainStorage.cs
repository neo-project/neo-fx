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
        IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> EnumerateStorage(in UInt160 scriptHash);
        bool TryGetAccount(in UInt160 key, out Account value);
        bool TryGetAsset(in UInt256 key, out Asset value);
        bool TryGetBlock(in UInt256 key, out Block value);
        bool TryGetBlock(uint index, out Block value);
        bool TryGetBlock(in UInt256 key, out BlockHeader header, out ReadOnlyMemory<UInt256> hashes);
        bool TryGetBlockHash(uint index, out UInt256 value);
        bool TryGetContract(in UInt160 key, out DeployedContract value);
        bool TryGetStorage(in StorageKey key, out StorageItem value);
        bool TryGetTransaction(in UInt256 key, out uint index, out Transaction tx);
        bool TryGetUnspentCoins(in UInt256 key, out ReadOnlyMemory<CoinState> value);
    }
}
