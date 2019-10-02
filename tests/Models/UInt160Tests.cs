using FluentAssertions;
using Xunit;

namespace NeoFx.Models.Tests
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

            UInt160.TryReadBytes(span, out var actual).Should().BeTrue();
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
            UInt160.TryReadBytes(span, out _).Should().BeFalse();
        }

        [Fact]
        public void Can_be_not_equal_to_null_of_same_type()
        {
            var a = new UInt160(0x0807060504030201, 0x100f0e0d0c0b0a09, 0x14131211);
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            a.Equals(null).Should().BeFalse();
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
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
    }
}
