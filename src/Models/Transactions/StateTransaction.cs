using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{

    public class StateTransaction : Transaction
    {
        public enum StateType : byte
        {
            Account = 0x40,
            Validator = 0x48
        }

        public class StateDescriptor
        {
            public StateType Type;
            public byte[] Key = Array.Empty<byte>();
            public string Field = string.Empty;
            public byte[] Value = Array.Empty<byte>();
        }

        public StateDescriptor[] Descriptors = Array.Empty<StateDescriptor>();

        public override TransactionType Type => TransactionType.StateTransaction;
    }
}
