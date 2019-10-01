using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{

    public class ClaimTransaction : Transaction
    {
        public override TransactionType Type => TransactionType.ClaimTransaction;

        public CoinReference[] Claims;
    }
}
