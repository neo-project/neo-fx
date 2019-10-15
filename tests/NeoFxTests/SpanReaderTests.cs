using FluentAssertions;
using NeoFx.Storage;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace NeoFxTests
{
    internal class BufferSegment<T> : ReadOnlySequenceSegment<T>
    {
        public BufferSegment(ReadOnlyMemory<T> memory)
        {
            Memory = memory;
        }

        public BufferSegment<T> Append(ReadOnlyMemory<T> memory)
        {
            var segment = new BufferSegment<T>(memory)
            {
                RunningIndex = RunningIndex + Memory.Length
            };
            Next = segment;
            return segment;
        }
    }

    public class SpanReaderTests
    {
        [Fact]
        public void SpanReader_Span_source()
        {
            Span<byte> buffer = stackalloc byte[] { 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8 };
            var reader = new SpanReader<byte>(buffer);

            reader.TryRead(out uint value1).Should().BeTrue();
            value1.Should().Be(0x04030201);
            reader.TryRead(out uint value2).Should().BeTrue();
            value2.Should().Be(0x08070605);
            reader.TryRead(out uint _).Should().BeFalse();
        }

        [Fact]
        public void SpanReader_Sequence_Source()
        {
            var buffer1 = new byte[] { 0x1, 0x2, 0x3, };
            var buffer2 = new byte[] { 0x4, 0x5, 0x6, 0x7, 0x8 };

            var first = new BufferSegment<byte>(buffer1);
            var last = first.Append(buffer2);
            var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);

            sequence.Length.Should().Be(buffer1.Length + buffer2.Length);
            sequence.FirstSpan.Length.Should().Be(buffer1.Length);

            var reader = new SpanReader<byte>(sequence);
            reader.TryRead(out uint value1).Should().BeTrue();
            value1.Should().Be(0x04030201);
            reader.TryRead(out uint value2).Should().BeTrue();
            value2.Should().Be(0x08070605);
            reader.TryRead(out uint _).Should().BeFalse();
        }

        [Fact]
        public void SpanReader_try_read_at_end_returns_false()
        {
            var buffer1 = new byte[] { 0x1, 0x2, 0x3, };
            var buffer2 = new byte[] { 0x4, 0x5, 0x6, 0x7, 0x8 };

            var first = new BufferSegment<byte>(buffer1);
            var last = first.Append(buffer2);
            var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);

            sequence.Length.Should().Be(buffer1.Length + buffer2.Length);
            sequence.FirstSpan.Length.Should().Be(buffer1.Length);

            var reader = new SpanReader<byte>(sequence);
            reader.TryAdvance(8).Should().BeTrue();
            reader.TryRead(out _).Should().BeFalse();
        }

        [Fact]
        public void SpanReader_can_read_each_element()
        {
            var buffer1 = new byte[] { 0x1, 0x2, 0x3, };
            var buffer2 = new byte[] { 0x4, 0x5, 0x6, 0x7, 0x8 };

            var first = new BufferSegment<byte>(buffer1);
            var last = first.Append(buffer2);
            var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);

            sequence.Length.Should().Be(buffer1.Length + buffer2.Length);
            sequence.FirstSpan.Length.Should().Be(buffer1.Length);

            byte b;
            var reader = new SpanReader<byte>(sequence);
            reader.TryRead(out b).Should().BeTrue();
            b.Should().Be(0x01);
            reader.TryRead(out b).Should().BeTrue();
            b.Should().Be(0x02);
            reader.TryRead(out b).Should().BeTrue();
            b.Should().Be(0x03);
            reader.TryRead(out b).Should().BeTrue();
            b.Should().Be(0x04);
            reader.TryRead(out b).Should().BeTrue();
            b.Should().Be(0x05);
            reader.TryRead(out b).Should().BeTrue();
            b.Should().Be(0x06);
            reader.TryRead(out b).Should().BeTrue();
            b.Should().Be(0x07);
            reader.TryRead(out b).Should().BeTrue();
            b.Should().Be(0x08);
            reader.TryRead(out _).Should().BeFalse();
        }

        [Fact]
        public void SpanReader_can_copy_all_elements()
        {
            var buffer1 = new byte[] { 0x1, 0x2, 0x3, };
            var buffer2 = new byte[] { 0x4, 0x5, 0x6, 0x7, 0x8 };

            var first = new BufferSegment<byte>(buffer1);
            var last = first.Append(buffer2);
            var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);

            sequence.Length.Should().Be(buffer1.Length + buffer2.Length);
            sequence.FirstSpan.Length.Should().Be(buffer1.Length);

            var reader = new SpanReader<byte>(sequence);

            Span<byte> expected = stackalloc byte[8] { 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8 };
            Span<byte> actual = stackalloc byte[8];

            reader.TryCopyTo(actual).Should().BeTrue();
            expected.SequenceEqual(actual).Should().BeTrue();
        }
    }
}
