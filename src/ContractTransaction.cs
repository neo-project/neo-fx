using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{

    public class ContractTransaction : Transaction
    {
        public override TransactionType Type => TransactionType.ContractTransaction;
    }
}
