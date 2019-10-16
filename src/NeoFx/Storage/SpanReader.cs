using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace NeoFx.Storage
{
    public ref struct SpanReader<T> where T : unmanaged
    {
        private ReadOnlySpan<T> span;
        private ReadOnlySequence<T> sequence;

        public SpanReader(ReadOnlySpan<T> span)
        {
            this.span = span;
            sequence = default;
        }

        public SpanReader(ReadOnlySequence<T> sequence)
        {
            this.sequence = sequence;
            span = sequence.FirstSpan;
        }

        public int Length => sequence.IsEmpty
            ? span.Length 
            : (int)sequence.Length;

        public readonly bool TryCopyTo(Span<T> destination)
        {
            var length = destination.Length;
            if (span.Length >= length
                && span.Slice(0, length).TryCopyTo(destination))
            {
                return true;
            }

            if (sequence.Length >= length)
            {
                sequence.Slice(0, length).CopyTo(destination);
                return true;
            }

            return false;
        }

        public bool TryRead(out T value)
        {
            if (span.Length >= 1)
            {
                value = span[0];
                return TryAdvance(1);
            }

            Debug.Assert(sequence.IsEmpty);

            value = default;
            return false;
        }

        public bool TryPeek(out T value)
        {
            if (span.Length >= 1)
            {
                value = span[0];
                return true;
            }

            Debug.Assert(sequence.IsEmpty);

            value = default;
            return false;
        }

        public delegate bool TryConvert<TValue>(ReadOnlySpan<T> span, [MaybeNullWhen(false)] out TValue value);

        public bool TryRead<TValue>(int size, TryConvert<TValue> tryConvert, [MaybeNullWhen(false)] out TValue value)
        {
            if (span.Length >= size
                && tryConvert(span.Slice(0, size), out value)
                && TryAdvance(size))
            {
                return true;
            }

            if (sequence.Length >= size)
            {
                using var memoryBlock = MemoryPool<T>.Shared.Rent(size);
                var memory = memoryBlock.Memory.Slice(0, size);
                if (TryCopyTo(memory.Span)
                    && tryConvert(memory.Span, out value)
                    && TryAdvance(size))
                {
                    return true;
                }
            }

            value = default!;
            return false;
        }

        public bool TryAdvance(int size)
        {
            if (sequence.IsEmpty && span.Length >= size)
            {
                span = span.Slice(size);
                return true;
            }

            if (sequence.Length >= size)
            {
                sequence = sequence.Slice(size);
                span = sequence.FirstSpan;
                return true;
            }

            return false;
        }
    }
}
