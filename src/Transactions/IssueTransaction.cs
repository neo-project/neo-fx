using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{

    public class IssueTransaction : Transaction
    {
        public override TransactionType Type => TransactionType.IssueTransaction;
    }
}
