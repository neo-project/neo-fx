using NeoFx.Models;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace NeoFx.Storage
{
    public ref struct SpanWriter<T>
    {
        private readonly Span<T> span;
        private int position;

        public SpanWriter(Span<T> span)
        {
            this.span = span;
            position = 0;
        }

        public Span<T> Span => span.Slice(position);
        public ReadOnlySpan<T> Contents => span.Slice(0, position);

        public int Length => span.Length - position;

        public void Advance(int size)
        {
            position += size;
        }
    }
}
