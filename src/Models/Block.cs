using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{
    public class Block
    {
        public BlockHeader Header = new BlockHeader();
        public Transaction[] Transactions = Array.Empty<Transaction>();
    }
}
