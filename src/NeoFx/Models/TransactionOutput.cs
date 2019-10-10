using System;
using System.Buffers;
using System.Buffers.Binary;

namespace NeoFx.Models
{
    public readonly struct TransactionOutput
    {
        public readonly UInt256 AssetId;
        public readonly Fixed8 Value;
        public readonly UInt160 ScriptHash;

        public TransactionOutput(in UInt256 assetId, Fixed8 value, in UInt160 scriptHash)
        {
            AssetId = assetId;
            Value = value;
            ScriptHash = scriptHash;
        }
    }
}
