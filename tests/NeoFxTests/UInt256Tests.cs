using FluentAssertions;
using NeoFx;
using System;
using Xunit;

namespace NeoFxTests
{
    public class UInt256Tests
    {
        [Fact]
        public void Is_zero_by_default()
        {
            var actual = new UInt256();
            (actual == UInt256.Zero).Should().BeTrue();
        }

        [Fact]
        public void Can_read_from_correct_size_byte_span()
        {
            byte[] span = new byte[] {
                0x01, 0x02, 0x03, 0x04,
                0x05, 0x06, 0x07, 0x08,
                0x09, 0x0a, 0x0b, 0x0c,
                0x0d, 0x0e, 0x0f, 0x10,
                0x11, 0x12, 0x13, 0x14,
                0x15, 0x16, 0x17, 0x18,
                0x19, 0x1a, 0x1b, 0x1c,
                0x1d, 0x1e, 0x1f, 0x20 };

            UInt256 expected = new UInt256(
                0x0807060504030201, 0x100f0e0d0c0b0a09,
                0x1817161514131211, 0x201f1e1d1c1b1a19);

            UInt256.TryRead(span, out var actual).Should().BeTrue();
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
            UInt256.TryRead(span, out _).Should().BeFalse();
        }

        [Fact]
        public void Can_be_not_equal_to_null_of_same_type()
        {
            var a = new UInt256(
                0x0807060504030201, 0x100f0e0d0c0b0a09,
                0x1817161514131211, 0x201f1e1d1c1b1a19);
            a.Equals(null!).Should().BeFalse();
        }

        [Fact]
        public void Can_be_equal_to_another_number_of_object_type()
        {
            var a = new UInt256(
                0x0807060504030201, 0x100f0e0d0c0b0a09,
                0x1817161514131211, 0x201f1e1d1c1b1a19);
            var o = (object)new UInt256(
                0x0807060504030201, 0x100f0e0d0c0b0a09,
                0x1817161514131211, 0x201f1e1d1c1b1a19);

            a.Equals(o).Should().BeTrue();
        }

        [Fact]
        public void Can_be_not_equal_to_another_number_of_object_type()
        {
            var a = new UInt256(
                0x0807060504030201, 0x100f0e0d0c0b0a09,
                0x1817161514131211, 0x201f1e1d1c1b1a19);
            object o = (object)1;

            a.Equals(o).Should().BeFalse();
        }

        [Fact]
        public void Can_be_greater_than_another_number()
        {
            var a = new UInt256(
                0x0807060504030201, 0x100f0e0d0c0b0a09,
                0x1817161514131211, 0x201f1e1d1c1b1a20);
            var b = new UInt256(
                0x0807060504030201, 0x100f0e0d0c0b0a09,
                0x1817161514131211, 0x201f1e1d1c1b1a19);

            (a > b).Should().BeTrue();
        }

        [Fact]
        public void Can_be_greater_than_another_number_or_equal()
        {
            var a = new UInt256(
                0x0807060504030201, 0x100f0e0d0c0b0a09,
                0x1817161514131211, 0x201f1e1d1c1b1a20);
            var b = new UInt256(
                0x0807060504030201, 0x100f0e0d0c0b0a09,
                0x1817161514131211, 0x201f1e1d1c1b1a19);

            (a >= b).Should().BeTrue();
        }

        [Fact]
        public void Can_be_less_than_another_number()
        {
            var a = new UInt256(
                0x0807060504030201, 0x100f0e0d0c0b0a09,
                0x1817161514131211, 0x201f1e1d1c1b1a18);
            var b = new UInt256(
                0x0807060504030201, 0x100f0e0d0c0b0a09,
                0x1817161514131211, 0x201f1e1d1c1b1a19);
            (a < b).Should().BeTrue();
        }

        [Fact]
        public void Can_be_less_than_another_number_or_equal()
        {
            var a = new UInt256(
                0x0807060504030201, 0x100f0e0d0c0b0a09,
                0x1817161514131211, 0x201f1e1d1c1b1a18);
            var b = new UInt256(
                0x0807060504030201, 0x100f0e0d0c0b0a09,
                0x1817161514131211, 0x201f1e1d1c1b1a19);
            (a <= b).Should().BeTrue();
        }

        [Fact]
        public void Can_TryParse()
        {
            var a = new UInt256(
                0x0807060504030201, 0x100f0e0d0c0b0a09,
                0x1817161514131211, 0x201f1e1d1c1b1a18);
            const string @string = "0x201f1e1d1c1b1a181817161514131211100f0e0d0c0b0a090807060504030201";
            UInt256.TryParse(@string, out var b).Should().BeTrue();

            a.Equals(b).Should().BeTrue();
        }

        [Fact]
        public void TryParse_invalid_string_returns_false()
        {
            const string @string = "0x201f1e1d1c1b1a1Q1817161514131211100f0e0d0c0b0a090807060504030201";
            UInt256.TryParse(@string, out _).Should().BeFalse();
        }

        [Fact]
        public void Can_TryFormat()
        {
            var a = new UInt256(
                0x0807060504030201, 0x100f0e0d0c0b0a09,
                0x1817161514131211, 0x201f1e1d1c1b1a18);

            var actual = new char[2 + (UInt256.Size * 2)];
            a.TryFormat(actual, out var written).Should().BeTrue();

            const string expected = "0x201f1e1d1c1b1a181817161514131211100f0e0d0c0b0a090807060504030201";
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
                0x11, 0x12, 0x13, 0x14,
                0x15, 0x16, 0x17, 0x18,
                0x19, 0x1a, 0x1b, 0x1c,
                0x1d, 0x1e, 0x1f, 0x20 };

            var fx = new UInt256(span);
            var neo = new Neo.UInt256(span);

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
                0x11, 0x12, 0x13, 0x14,
                0x15, 0x16, 0x17, 0x18,
                0x19, 0x1a, 0x1b, 0x1c,
                0x1d, 0x1e, 0x1f, 0x20 };

            var fx = new UInt256(span);
            var neo = new Neo.UInt256(span);

            byte[] buffer = new byte[UInt256.Size];
            fx.TryWrite(buffer).Should().BeTrue();
            buffer.AsSpan().SequenceEqual(neo.ToArray()).Should().BeTrue();
        }

        [Fact]
        public void TryParse_matches_NEO()
        {
            const string @string = "0a372ac8f778eeebb1ccdbb250fe596b83d1d1b9f366d71dfd4c53956bed5cce";

            Neo.UInt256.TryParse(@string, out var neo).Should().BeTrue();
            UInt256.TryParse(@string, out var fx).Should().BeTrue();

            byte[] buffer = new byte[UInt256.Size];
            fx.TryWrite(buffer).Should().BeTrue();
            buffer.AsSpan().SequenceEqual(neo.ToArray()).Should().BeTrue();
            (fx.ToString() == neo.ToString()).Should().BeTrue();
        }

        [Fact]
        public void TryParse_0x_matches_NEO()
        {
            const string @string = "0x0a372ac8f778eeebb1ccdbb250fe596b83d1d1b9f366d71dfd4c53956bed5cce";

            Neo.UInt256.TryParse(@string, out var neo).Should().BeTrue();
            UInt256.TryParse(@string, out var fx).Should().BeTrue();

            byte[] buffer = new byte[UInt256.Size];
            fx.TryWrite(buffer).Should().BeTrue();
            buffer.AsSpan().SequenceEqual(neo.ToArray()).Should().BeTrue();
            (fx.ToString() == neo.ToString()).Should().BeTrue();
        }
    }
}
