using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{
    public abstract class Transaction
    {
        public enum TransactionType : byte
        {
            MinerTransaction = 0x00,
            IssueTransaction = 0x01,
            ClaimTransaction = 0x02,
            EnrollmentTransaction = 0x20,
            RegisterTransaction = 0x40,
            ContractTransaction = 0x80,
            StateTransaction = 0x90,
            PublishTransaction = 0xd0,
            InvocationTransaction = 0xd1
        }

        public abstract TransactionType Type { get; }
        public byte Version;
        public TransactionAttribute[] Attributes = Array.Empty<TransactionAttribute>();
        public CoinReference[] Inputs = Array.Empty<CoinReference>();
        public TransactionOutput[] Outputs = Array.Empty<TransactionOutput>();
        public Witness[] Witnesses = Array.Empty<Witness>();
    }
}
