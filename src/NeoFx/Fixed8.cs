using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NeoFx
{
    public readonly struct Fixed8 : IComparable<Fixed8>, IEquatable<Fixed8>
    {
        private const long D = 100_000_000;
        public const int Size = sizeof(long);

        public static readonly Fixed8 MaxValue = new Fixed8(long.MaxValue);
        public static readonly Fixed8 MinValue = new Fixed8(long.MinValue);
        public static readonly Fixed8 One = new Fixed8(D);
        public static readonly Fixed8 Satoshi = new Fixed8(1);
        public static readonly Fixed8 Zero;

        private readonly long value;

        public Fixed8(long value)
        {
            this.value = value;
        }

        public static bool TryRead(ReadOnlySpan<byte> buffer, out Fixed8 result)
        {
            if (BinaryPrimitives.TryReadInt64LittleEndian(buffer, out var @long))
            {
                result = new Fixed8(@long);
                return true;
            }

            result = default;
            return false;
        }

        public bool TryWrite(Span<byte> buffer)
        {
            return BinaryPrimitives.TryWriteInt64LittleEndian(buffer, value);
        }

        public static Fixed8 FromDecimal(decimal value)
        {
            value *= D;
            if (value < long.MinValue || value > long.MaxValue)
                throw new OverflowException();
            return new Fixed8((long)value);
        }

        public Fixed8 Abs()
        {
            if (value >= 0) return this;
            return new Fixed8(-value);
        }

        public Fixed8 Ceiling()
        {
            long remainder = value % D;
            if (remainder == 0)
            {
                return this;
            }

            if (remainder > 0)
            {
                return new Fixed8(value - remainder + D);
            }

            return new Fixed8(value - remainder);
        }

        public static explicit operator decimal(Fixed8 value)
        {
            return value.value / (decimal)D;
        }

        public static explicit operator long(Fixed8 value)
        {
            return value.value / D;
        }

        public override string ToString()
        {
            return ((decimal)this).ToString(CultureInfo.InvariantCulture);
        }

        public string ToString(string format)
        {
            return ((decimal)this).ToString(format);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return ((decimal)this).ToString(format, formatProvider);
        }

        public static bool TryParse(string @string, out Fixed8 result)
        {
            if (decimal.TryParse(@string, NumberStyles.Float, CultureInfo.InvariantCulture, out var @decimal))
            {
                result = FromDecimal(@decimal);
                return true;
            }

            result = default;
            return false;
        }

        public static Fixed8 Parse(string @string)
        {
            if (TryParse(@string, out var result))
            {
                return result;
            }

            throw new ArgumentException(nameof(@string));
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return (obj is Fixed8 value) && (Equals(value));
        }

        public int CompareTo(Fixed8 other)
        {
            return value.CompareTo(other.value);
        }

        public bool Equals(Fixed8 other)
        {
            return value.Equals(other.value);
        }

        public static Fixed8 operator -(Fixed8 value)
        {
            return new Fixed8(-value.value);
        }

        public static Fixed8 operator +(Fixed8 x, Fixed8 y)
        {
            return new Fixed8(checked(x.value + y.value));
        }

        public static Fixed8 operator -(Fixed8 x, Fixed8 y)
        {
            return new Fixed8(checked(x.value - y.value));
        }

        public static Fixed8 operator *(Fixed8 x, long y)
        {
            return new Fixed8(checked(x.value * y));
        }

        public static Fixed8 operator *(Fixed8 x, Fixed8 y)
        {
            const ulong QUO = (1ul << 63) / (D >> 1);
            const ulong REM = ((1ul << 63) % (D >> 1)) << 1;

            int sign = Math.Sign(x.value) * Math.Sign(y.value);
            ulong ux = (ulong)Math.Abs(x.value);
            ulong uy = (ulong)Math.Abs(y.value);
            ulong xh = ux >> 32;
            ulong xl = ux & 0x00000000fffffffful;
            ulong yh = uy >> 32;
            ulong yl = uy & 0x00000000fffffffful;
            ulong rh = xh * yh;
            ulong rm = (xh * yl) + (xl * yh);
            ulong rl = xl * yl;
            ulong rmh = rm >> 32;
            ulong rml = rm << 32;
            rh += rmh;
            rl += rml;
            if (rl < rml)
            {
                ++rh;
            }

            if (rh >= D)
            {
                throw new OverflowException();
            }

            ulong rd = (rh * REM) + rl;
            if (rd < rl)
            {
                ++rh;
            }

            ulong r = (rh * QUO) + (rd / D);
            return new Fixed8((long)r * sign);
        }

        public static Fixed8 operator /(Fixed8 x, long y)
        {
            return new Fixed8(x.value / y);
        }

        public static bool operator ==(in Fixed8 left, in Fixed8 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in Fixed8 left, in Fixed8 right)
        {
            return !left.Equals(right);
        }

        public static bool operator >(in Fixed8 left, in Fixed8 right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(in Fixed8 left, in Fixed8 right)
        {
            return left.CompareTo(right) >= 0;
        }

        public static bool operator <(in Fixed8 left, in Fixed8 right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(in Fixed8 left, in Fixed8 right)
        {
            return left.CompareTo(right) <= 0;
        }
    }
}
