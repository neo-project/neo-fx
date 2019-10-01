using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{

    public class TransactionOutput
    {
        public UInt256 AssetId;
        public Fixed8 Value;
        public UInt160 ScriptHash;
    }
}
