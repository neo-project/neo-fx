using FluentAssertions;
using Xunit;

namespace NeoFx.Models.Tests
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

            UInt256.TryReadBytes(span, out var actual).Should().BeTrue();
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
            UInt256.TryReadBytes(span, out _).Should().BeFalse();
        }

        [Fact]
        public void Can_be_not_equal_to_null_of_same_type()
        {
            var a = new UInt256(
                0x0807060504030201, 0x100f0e0d0c0b0a09, 
                0x1817161514131211, 0x201f1e1d1c1b1a19);
            a.Equals(null).Should().BeFalse();
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
    }
}
