using FluentAssertions;
using NeoFx;
using System;
using Xunit;

namespace NeoFxTests
{
    public class UInt160Tests
    {
        [Fact]
        public void Is_zero_by_default()
        {
            var actual = new UInt160();
            (actual == UInt160.Zero).Should().BeTrue();
        }

        [Fact]
        public void Can_read_from_correct_size_byte_span()
        {
            byte[] span = new byte[] {
                0x01, 0x02, 0x03, 0x04,
                0x05, 0x06, 0x07, 0x08,
                0x09, 0x0a, 0x0b, 0x0c,
                0x0d, 0x0e, 0x0f, 0x10,
                0x11, 0x12, 0x13, 0x14 };

            UInt160 expected = new UInt160(
                0x0807060504030201, 0x100f0e0d0c0b0a09, 0x14131211);

            UInt160.TryRead(span, out var actual).Should().BeTrue();
            actual.Should().Be(expected);
        }

        [Fact]
        public void Fail_to_read_from_incorrect_size_byte_span()
        {
            var span = new byte[] {
                0x01, 0x02, 0x03, 0x04,
                0x05, 0x06, 0x07, 0x08,
                0x09, 0x0a, 0x0b, 0x0c,
                0x0d, 0x0e, 0x0f, 0x10,
                0x11, 0x12, 0x13};
            UInt160.TryRead(span, out _).Should().BeFalse();
        }

        [Fact]
        public void Can_be_not_equal_to_null_of_same_type()
        {
            var a = new UInt160(0x0807060504030201, 0x100f0e0d0c0b0a09, 0x14131211);
            a.Equals(null!).Should().BeFalse();
        }

        [Fact]
        public void Can_be_equal_to_another_number_of_object_type()
        {
            var a = new UInt160(0x0807060504030201, 0x100f0e0d0c0b0a09, 0x14131211);
            var o = (object)new UInt160(0x0807060504030201, 0x100f0e0d0c0b0a09, 0x14131211);

            a.Equals(o).Should().BeTrue();
        }

        [Fact]
        public void Can_be_not_equal_to_another_number_of_object_type()
        {
            var a = new UInt160(0x0807060504030201, 0x100f0e0d0c0b0a09, 0x14131211);
            object o = (object)1;

            a.Equals(o).Should().BeFalse();
        }

        [Fact]
        public void Can_be_greater_than_another_number()
        {
            var a = new UInt160(0x0807060504030201, 0x100f0e0d0c0b0a09, 0x14131212);
            var b = new UInt160(0x0807060504030201, 0x100f0e0d0c0b0a09, 0x14131211);

            (a > b).Should().BeTrue();
        }

        [Fact]
        public void Can_be_greater_than_another_number_or_equal()
        {
            var a = new UInt160(0x0807060504030201, 0x100f0e0d0c0b0a09, 0x14131212);
            var b = new UInt160(0x0807060504030201, 0x100f0e0d0c0b0a09, 0x14131211);

            (a >= b).Should().BeTrue();
        }

        [Fact]
        public void Can_be_less_than_another_number()
        {
            var a = new UInt160(0x0807060504030201, 0x100f0e0d0c0b0a09, 0x14131210);
            var b = new UInt160(0x0807060504030201, 0x100f0e0d0c0b0a09, 0x14131211);
            (a < b).Should().BeTrue();
        }

        [Fact]
        public void Can_be_less_than_another_number_or_equal()
        {
            var a = new UInt160(0x0807060504030201, 0x100f0e0d0c0b0a09, 0x14131210);
            var b = new UInt160(0x0807060504030201, 0x100f0e0d0c0b0a09, 0x14131211);
            (a <= b).Should().BeTrue();
        }

        [Fact]
        public void Can_TryParse()
        {
            UInt160 a = new UInt160(0x0807060504030201, 0x100f0e0d0c0b0a09, 0x14131211);
            const string @string = "0x14131211100f0e0d0c0b0a090807060504030201";
            UInt160.TryParse(@string, out var b).Should().BeTrue();

            a.Equals(b).Should().BeTrue();
        }

        [Fact]
        public void TryParse_invalid_string_returns_false()
        {
            const string @string = "0x14131211100f0e0Qd0c0b0a090807060504030201";
            UInt160.TryParse(@string, out _).Should().BeFalse();
        }

        [Fact]
        public void Can_TryFormat()
        {
            UInt160 a = new UInt160(0x0807060504030201, 0x100f0e0d0c0b0a09, 0x14131211);

            var actual = new char[2 + (UInt160.Size * 2)];
            a.TryFormat(actual, out var written).Should().BeTrue();

            const string expected = "0x14131211100f0e0d0c0b0a090807060504030201";
            written.Should().Be(expected.Length);

            actual.Should().Equal(expected, (l, r) => l == r);
        }




        [Fact]
        public void ToString_matches_NEO_ToString()
        {
            byte[] span = new byte[] {
                0x01, 0x02, 0x03, 0x04,
                0x05, 0x06, 0x07, 0x08,
                0x09, 0x0a, 0x0b, 0x0c,
                0x0d, 0x0e, 0x0f, 0x10,
                0x11, 0x12, 0x13, 0x14 };

            var fx = new UInt160(span);
            var neo = new Neo.UInt160(span);

            (fx.ToString() == neo.ToString()).Should().BeTrue();
        }

        [Fact]
        public void TryWriteBytes_matches_NEO_ToArray()
        {
            byte[] span = new byte[] {
                0x01, 0x02, 0x03, 0x04,
                0x05, 0x06, 0x07, 0x08,
                0x09, 0x0a, 0x0b, 0x0c,
                0x0d, 0x0e, 0x0f, 0x10,
                0x11, 0x12, 0x13, 0x14 };

            var fx = new UInt160(span);
            var neo = new Neo.UInt160(span);

            byte[] buffer = new byte[UInt160.Size];
            fx.TryWrite(buffer).Should().BeTrue();
            buffer.AsSpan().SequenceEqual(neo.ToArray()).Should().BeTrue();
        }

        [Fact]
        public void TryParse_matches_NEO()
        {
            const string @string = "30f41a14ca6019038b055b585d002b287b5fdd47";

            Neo.UInt160.TryParse(@string, out var neo).Should().BeTrue();
            UInt160.TryParse(@string, out var fx).Should().BeTrue();

            byte[] buffer = new byte[UInt160.Size];
            fx.TryWrite(buffer).Should().BeTrue();
            buffer.AsSpan().SequenceEqual(neo.ToArray()).Should().BeTrue();
            (fx.ToString() == neo.ToString()).Should().BeTrue();
        }

        [Fact]
        public void TryParse_0x_matches_NEO()
        {
            const string @string = "0x30f41a14ca6019038b055b585d002b287b5fdd47";

            Neo.UInt160.TryParse(@string, out var neo).Should().BeTrue();
            UInt160.TryParse(@string, out var fx).Should().BeTrue();

            byte[] buffer = new byte[UInt160.Size];
            fx.TryWrite(buffer).Should().BeTrue();
            buffer.AsSpan().SequenceEqual(neo.ToArray()).Should().BeTrue();
            (fx.ToString() == neo.ToString()).Should().BeTrue();
        }
    }
}
