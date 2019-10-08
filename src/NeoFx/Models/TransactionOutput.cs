using System;
using System.Buffers;
using System.Buffers.Binary;

namespace NeoFx.Models
{
    public readonly struct TransactionOutput
    {
        public readonly UInt256 AssetId;
        public readonly /*Fixed8*/ long Value;
        public readonly UInt160 ScriptHash;

        public TransactionOutput(in UInt256 assetId, /*Fixed8 */long value, in UInt160 scriptHash)
        {
            AssetId = assetId;
            Value = value;
            ScriptHash = scriptHash;
        }
    }
}
