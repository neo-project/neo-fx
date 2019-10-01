using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{

    public struct Fixed8
    {
        private const long D = 100_000_000;

        internal long value;

        public static readonly Fixed8 MaxValue = new Fixed8 { value = long.MaxValue };

        public static readonly Fixed8 MinValue = new Fixed8 { value = long.MinValue };

        public static readonly Fixed8 One = new Fixed8 { value = D };

        public static readonly Fixed8 Satoshi = new Fixed8 { value = 1 };

        public static readonly Fixed8 Zero = default(Fixed8);

        public int Size => sizeof(long);
        public long Value => value;

        public static explicit operator decimal(Fixed8 value)
        {
            return value.value / (decimal)D;
        }

        public static explicit operator long(Fixed8 value)
        {
            return value.value / D;
        }
    }
}
