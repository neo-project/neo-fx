using DevHawk.Buffers;
using NeoFx.Storage;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;

namespace NeoFx
{
    // TODO: IFormattable?

    public readonly struct UInt256 : IEquatable<UInt256>, IComparable<UInt256>, IWritable<UInt256>
    {
        public static readonly UInt256 Zero = new UInt256(0, 0, 0, 0);

        private readonly ulong data1;
        private readonly ulong data2;
        private readonly ulong data3;
        private readonly ulong data4;

        public const int Size = 4 * sizeof(ulong);

        internal UInt256(ulong data1, ulong data2, ulong data3, ulong data4)
        {
            this.data1 = data1;
            this.data2 = data2;
            this.data3 = data3;
            this.data4 = data4;
        }

        public UInt256(ReadOnlySpan<byte> span)
        {
            if (!TryRead(span, out this))
            {
                throw new ArgumentException(nameof(span));
            }
        }

        public static bool TryRead(ReadOnlySpan<byte> buffer, out UInt256 result)
        {
            if (buffer.Length >= Size
                && BinaryPrimitives.TryReadUInt64LittleEndian(buffer, out var data1)
                && BinaryPrimitives.TryReadUInt64LittleEndian(buffer.Slice(8), out var data2)
                && BinaryPrimitives.TryReadUInt64LittleEndian(buffer.Slice(16), out var data3)
                && BinaryPrimitives.TryReadUInt64LittleEndian(buffer.Slice(24), out var data4))
            {
                result = new UInt256(data1, data2, data3, data4);
                return true;
            }

            result = default;
            return false;
        }

        public static bool TryRead(ref BufferReader<byte> reader, out UInt256 result)
        {
            if (reader.TryReadLittleEndian(out ulong data1)
                && reader.TryReadLittleEndian(out ulong data2)
                && reader.TryReadLittleEndian(out ulong data3)
                && reader.TryReadLittleEndian(out ulong data4))
            {
                result = new UInt256(data1, data2, data3, data4);
                return true;
            }

            result = default;
            return false;
        }

        public bool TryWrite(Span<byte> buffer)
        {
            return buffer.Length >= Size
                && BinaryPrimitives.TryWriteUInt64LittleEndian(buffer, data1)
                && BinaryPrimitives.TryWriteUInt64LittleEndian(buffer.Slice(8), data2)
                && BinaryPrimitives.TryWriteUInt64LittleEndian(buffer.Slice(16), data3)
                && BinaryPrimitives.TryWriteUInt64LittleEndian(buffer.Slice(24), data4);
        }

        public void Write(Span<byte> buffer)
        {
            if (!TryWrite(buffer))
                throw new ArgumentException(nameof(buffer));
        }

        public void Write(IBufferWriter<byte> writer)
        {
            writer.WriteLittleEndian(data1);
            writer.WriteLittleEndian(data2);
            writer.WriteLittleEndian(data3);
            writer.WriteLittleEndian(data4);
        }

        public override string ToString()
        {
            return string.Create(2 + (Size * 2), this, (buffer, that) =>
            {
                bool result = that.TryFormat(buffer, out var charWritten);
                Debug.Assert(result && charWritten == (2 + (Size * 2)));
            });
        }

        public bool TryFormat(Span<char> destination, out int charsWritten)
        {
            // TODO: add ReadOnlySpan<char> format && IFormatProvider arguments

            if (destination.Length >= ((Size * 2) + 2)
                && data4.TryFormat(destination.Slice(2), out var data4Written, "x16")
                && data3.TryFormat(destination.Slice(18), out var data3Written, "x16")
                && data2.TryFormat(destination.Slice(34), out var data2Written, "x16")
                && data1.TryFormat(destination.Slice(50), out var data1Written, "x16"))
            {
                Debug.Assert(data1Written == 16);
                Debug.Assert(data2Written == 16);
                Debug.Assert(data3Written == 16);
                Debug.Assert(data4Written == 16);

                destination[0] = '0';
                destination[1] = 'x';
                charsWritten = ((Size * 2) + 2);
                return true;
            }

            charsWritten = 0;
            return false;
        }

        public static bool TryParse(ReadOnlySpan<char> @string, out UInt256 result)
        {
            @string = @string.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase)
                ? @string.Slice(2) : @string;

            if (@string.Length == (Size * 2) &&
                ulong.TryParse(@string.Slice(0, 16), NumberStyles.AllowHexSpecifier, null, out var d4) &&
                ulong.TryParse(@string.Slice(16, 16), NumberStyles.AllowHexSpecifier, null, out var d3) &&
                ulong.TryParse(@string.Slice(32, 16), NumberStyles.AllowHexSpecifier, null, out var d2) &&
                ulong.TryParse(@string.Slice(48, 16), NumberStyles.AllowHexSpecifier, null, out var d1))
            {
                result = new UInt256(d1, d2, d3, d4);
                return true;
            }

            result = default;
            return false;
        }

        public static UInt256 Parse(ReadOnlySpan<char> @string)
        {
            if (TryParse(@string, out var value))
            {
                return value;
            }

            throw new ArgumentException(nameof(@string));
        }

        public override bool Equals(object obj)
        {
            return (obj is UInt256 value) && (this.Equals(value));
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(data1, data2, data3, data4);
        }

        public bool Equals(in UInt256 other)
        {
            return (data1 == other.data1)
                && (data2 == other.data2)
                && (data3 == other.data3)
                && (data4 == other.data4);
        }

        public int CompareTo(in UInt256 other)
        {
            var result = data1.CompareTo(other.data1);
            if (result != 0)
                return result;

            result = data2.CompareTo(other.data2);
            if (result != 0)
                return result;

            result = data3.CompareTo(other.data3);
            if (result != 0)
                return result;

            return data4.CompareTo(other.data4);
        }

        int IComparable<UInt256>.CompareTo(UInt256 other)
        {
            return this.CompareTo(other);
        }

        bool IEquatable<UInt256>.Equals(UInt256 other)
        {
            return this.Equals(other);
        }

        public static bool operator ==(in UInt256 left, in UInt256 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in UInt256 left, in UInt256 right)
        {
            return !left.Equals(right);
        }

        public static bool operator >(in UInt256 left, in UInt256 right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(in UInt256 left, in UInt256 right)
        {
            return left.CompareTo(right) >= 0;
        }

        public static bool operator <(in UInt256 left, in UInt256 right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(in UInt256 left, in UInt256 right)
        {
            return left.CompareTo(right) <= 0;
        }
    }
}
