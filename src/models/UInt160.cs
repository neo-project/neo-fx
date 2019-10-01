using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{
    public readonly struct UInt160 : IEquatable<UInt160>, IComparable<UInt160>
    {
        public static readonly UInt160 Zero = new UInt160(0, 0, 0);

        private readonly ulong data1;
        private readonly ulong data2;
        private readonly uint data3;

        private UInt160(ulong data1, ulong data2, uint data3)
        {
            this.data1 = data1;
            this.data2 = data2;
            this.data3 = data3;
        }

        public UInt160(ReadOnlySpan<byte> span)
        {
            if (!TryReadBytes(span, out this))
            {
                throw new ArgumentException(nameof(span));
            }
        }

        public static bool TryReadBytes(ReadOnlySpan<byte> buffer, out UInt160 result)
        {
            if (buffer.Length >= 20
                && BinaryPrimitives.TryReadUInt64LittleEndian(buffer, out var data1)
                && BinaryPrimitives.TryReadUInt64LittleEndian(buffer.Slice(8), out var data2)
                && BinaryPrimitives.TryReadUInt32LittleEndian(buffer.Slice(16), out var data3))
            {
                result = new UInt160(data1, data2, data3);
                return true;
            }

            result = default;
            return false;
        }

        // TODO:
        //      public bool TryWriteBytes(Span<byte> buffer)
        //      public static bool TryParse(ReadOnlySpan<char> @string, out UInt160 result)
        //      public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider provider = null)
        //      public override string ToString()

        public override bool Equals(object obj)
        {
            return (obj is UInt160 value) && (Equals(value));
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(data1, data2, data3);
        }

        public bool Equals(in UInt160 other)
        {
            return (data1 == other.data1)
                && (data2 == other.data2)
                && (data3 == other.data3);
        }

        public int CompareTo(in UInt160 other)
        {
            var result = data1.CompareTo(other.data1);
            if (result != 0)
                return result;

            result = data2.CompareTo(other.data2);
            if (result != 0)
                return result;

            return data3.CompareTo(other.data3);
        }

        int IComparable<UInt160>.CompareTo(UInt160 other)
        {
            return CompareTo(other);
        }

        bool IEquatable<UInt160>.Equals(UInt160 other)
        {
            return Equals(other);
        }

        public static bool operator ==(in UInt160 left, in UInt160 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in UInt160 left, in UInt160 right)
        {
            return !left.Equals(right);
        }

        public static bool operator >(in UInt160 left, in UInt160 right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(in UInt160 left, in UInt160 right)
        {
            return left.CompareTo(right) >= 0;
        }

        public static bool operator <(in UInt160 left, in UInt160 right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(in UInt160 left, in UInt160 right)
        {
            return left.CompareTo(right) <= 0;
        }
    }
}
