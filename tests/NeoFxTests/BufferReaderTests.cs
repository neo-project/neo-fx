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

    public class BufferReaderTests
    {
        static ReadOnlySequence<byte> GetTestReadOnlySequence()
        {
            var seq = new Nerdbank.Streams.Sequence<byte>();

            var mem1 = seq.GetMemory(4);
            var span1 = mem1.Span;
            span1[0] = 0;
            span1[1] = 1;
            span1[2] = 2;
            span1[3] = 3;
            seq.Advance(4);

            var mem2 = seq.GetMemory(6);
            var span2 = mem2.Span;
            span2[0] = 4;
            span2[1] = 5;
            span2[2] = 6;
            span2[3] = 7;
            span2[4] = 8;
            span2[5] = 9;
            seq.Advance(6);

            return seq.AsReadOnlySequence;
        }

        [Fact]
        public void Can_TryRead_Span()
        {
            Span<byte> span = new byte[] {0,1,2,3,4,5,6,7,8,9};
            var reader = new BufferReader<byte>(span);

            for (byte expected = 0; expected < 10; expected++)
            {
                reader.TryRead(out byte actual).Should().BeTrue();
                actual.Should().Be(expected);
            }

            reader.TryRead(out byte _).Should().BeFalse();
        }

        [Fact]
        public void Can_TryPeek_Span()
        {
            Span<byte> span = new byte[] {0,1,2,3,4,5,6,7,8,9};
            var reader = new BufferReader<byte>(span);

            reader.TryPeek(out byte actual).Should().BeTrue();
            actual.Should().Be(0x00);
            reader.TryPeek(out byte actual2).Should().BeTrue();
            actual2.Should().Be(0x00);
        }

        [Fact]
        public void Can_TryPeekAndAdvance_Span()
        {
            Span<byte> span = new byte[] {0,1,2,3,4,5,6,7,8,9};
            var reader = new BufferReader<byte>(span);

            for (byte expected = 0; expected < 10; expected++)
            {
                reader.TryPeek(out byte actual).Should().BeTrue();
                actual.Should().Be(expected);
                reader.Advance(1);
            }

            reader.TryRead(out byte _).Should().BeFalse();
        }

        [Fact]
        public void Can_TryRead_Sequence()
        {
            var seq = GetTestReadOnlySequence();
            var reader = new BufferReader<byte>(seq);

            for (byte expected = 0; expected < 10; expected++)
            {
                reader.TryRead(out byte actual).Should().BeTrue();
                actual.Should().Be(expected);
            }

            reader.TryRead(out byte _).Should().BeFalse();
        }

        [Fact]
        public void Can_TryPeek_Sequence()
        {
            var seq = GetTestReadOnlySequence();
            var reader = new BufferReader<byte>(seq);

            reader.TryPeek(out byte actual).Should().BeTrue();
            actual.Should().Be(0x00);
            reader.TryPeek(out byte actual2).Should().BeTrue();
            actual2.Should().Be(0x00);
        }

        [Fact]
        public void Can_TryPeekAndAdvance_Sequence()
        {
            var seq = GetTestReadOnlySequence();
            var reader = new BufferReader<byte>(seq);

            for (byte expected = 0; expected < 10; expected++)
            {
                reader.TryPeek(out byte actual).Should().BeTrue();
                actual.Should().Be(expected);
                reader.Advance(1);
            }

            reader.TryRead(out byte _).Should().BeFalse();
        }

        [Fact]
        public void Can_TryCopyTo_Seq()
        {
            Span<byte> expected = new byte[] {0,1,2,3,4,5,6,7,8,9};

            var ros = GetTestReadOnlySequence();
            var reader = new BufferReader<byte>(ros);

            Span<byte> actual = new byte[10];
            reader.TryCopyTo(actual).Should().BeTrue();

            actual.SequenceEqual(expected).Should().BeTrue();
        }

        [Fact]
        public void Can_TryCopyTo_Span()
        {
            Span<byte> span = new byte[] {0,1,2,3,4,5,6,7,8,9};
            var reader = new BufferReader<byte>(span);

            Span<byte> expected = new byte[] {0,1,2,3,4,5,6,7,8,9};

            Span<byte> actual = new byte[10];
            reader.TryCopyTo(actual).Should().BeTrue();
            actual.SequenceEqual(expected).Should().BeTrue();
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

            var reader = new BufferReader<byte>(sequence);
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

            var reader = new BufferReader<byte>(sequence);
            reader.Advance(8);
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
            var reader = new BufferReader<byte>(sequence);
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

            var reader = new BufferReader<byte>(sequence);

            Span<byte> expected = stackalloc byte[8] { 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8 };
            Span<byte> actual = stackalloc byte[8];

            reader.TryCopyTo(actual).Should().BeTrue();
            expected.SequenceEqual(actual).Should().BeTrue();
        }
    }
}
