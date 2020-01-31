using NeoFx.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace NeoFx
{
    public static class OpCode
    {
        public const byte PUSH0 = 0x00;
        public const byte PUSHF = 0x00;
        public const byte PUSHBYTES75= 0x4B;
        public const byte PUSHDATA1 = 0x4C;
        public const byte PUSHDATA2 = 0x4D;
        public const byte PUSHDATA4 = 0x4E;
        public const byte PUSHM1 = 0x4F;
        public const byte PUSHT = 0x51;
        public const byte CHECKMULTISIG = 0xAE;
        public const byte CHECKSIG = 0xAC;
    }
}
