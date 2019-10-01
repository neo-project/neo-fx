using NeoFx.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Abstractions
{
    public class BlockState 
    {
        public long SystemFeeAmount;

    }
    public class TransactionState { }
    public class AccountState { }
    public class UnspentCoinState { }
    public class SpentCoinState { }
    public class ValidatorState { }
    public class AssetState { }
    public class ContractState { }

    public class StorageKey
    {
        public UInt160 ScriptHash;
        public byte[] Key;
    }

    public class StorageItem { }
    public class HeaderHashList { }

    public interface IStorage
    {
        IDictionary<UInt256, BlockState> Blocks { get; }
        IDictionary<UInt256, TransactionState> Transactions { get; }
        IDictionary<UInt160, AccountState> Accounts { get; }
        IDictionary<UInt256, UnspentCoinState> UnspentCoins { get; }
        IDictionary<UInt256, SpentCoinState> SpentCoins { get; }
        IDictionary<byte[], ValidatorState> Validators { get; }
        IDictionary<UInt256, AssetState> Assets { get; }
        IDictionary<UInt160, ContractState> Contracts { get; }
        IDictionary<StorageKey, StorageItem> Storages { get; }
        IDictionary<uint, HeaderHashList> HeaderHashList { get; }

        Fixed8[] ValidatorsCount { get; set; }
        (UInt256 Hash, uint Index) BlockHashIndex { get; set; }
        (UInt256 Hash, uint Index) HeaderHashIndex { get; set; }
    }
}
