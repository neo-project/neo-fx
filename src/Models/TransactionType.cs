using System;
using System.Buffers;

namespace NeoFx.Models
{
    public enum TransactionType : byte
    {
        Miner = 0x00,
        Issue = 0x01,
        Claim = 0x02,
        Enrollment = 0x20,
        Register = 0x40,
        Contract = 0x80,
        State = 0x90,
        Publish = 0xd0,
        Invocation = 0xd1
    }
}
