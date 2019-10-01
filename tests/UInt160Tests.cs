using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace NeoFx.Models.Tests
{
    public class UInt160Tests
    {
        [Fact]
        public void is_zero_by_default()
        {
            var actual = new UInt160();
            (actual == UInt160.Zero).Should().BeTrue();
        }

        [Fact]
        public void can_read_from_correct_size_byte_span()
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
    }
}
