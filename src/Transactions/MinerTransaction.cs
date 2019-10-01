using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{
    public class MinerTransaction : Transaction
    {
        public override TransactionType Type => TransactionType.MinerTransaction;

        public uint Nonce;
    }
}
