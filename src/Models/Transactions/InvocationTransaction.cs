using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{

    public class InvocationTransaction : Transaction
    {
        public override TransactionType Type => TransactionType.InvocationTransaction;

        public byte[] Script = Array.Empty<byte>();
        public Fixed8 Gas;
    }
}
