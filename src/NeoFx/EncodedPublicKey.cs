using NeoFx.Storage;
using System;
using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;

namespace NeoFx
{
    // TODO: this type should hold either a compressed or uncompressed public key in 
    //       a fixed size buffer
    public readonly struct EncodedPublicKey : IWritable<EncodedPublicKey>
    {
        public readonly ImmutableArray<byte> Key;

        public int Size => Key.Length;

        public EncodedPublicKey(ImmutableArray<byte> key)
        {
            Key = key;
        }

        public EncodedPublicKey(ECPoint point, bool compressed)
        {
            if (!TryEncode(point, compressed, out this))
            {
                throw new ArgumentException(nameof(point));
            }
        }

        public bool TryDecode(ECCurve curve, out ECPoint point)
        {
            return curve.TryDecodePoint(Key.AsSpan(), out point);
        }

        public static bool TryEncode(ECPoint point, bool compressed, out EncodedPublicKey value)
        {
            if (point.TryEncodePoint(compressed, out var encodedPoint))
            {
                value = new EncodedPublicKey(encodedPoint);
                return true;
            }

            value = default;
            return false;
        }

        //public bool TryWrite(Span<byte> buffer)
        //{
        //    return Key.Span.TryCopyTo(buffer);
        //}

        //public void Write(Span<byte> buffer)
        //{
        //    if (!TryWrite(buffer))
        //        throw new ArgumentException(nameof(buffer));
        //}

        public static bool TryRead(ref BufferReader<byte> reader, out EncodedPublicKey value)
        {
            static bool TryGetBufferLength(byte type, out int length)
            {
                length = type switch
                {
                    0x00 => 1,
                    var x when (0x02 <= x && x <= 0x03) => 33,
                    var x when (0x04 <= x && x <= 0x06) => 65,
                    _ => 0
                };

                return length > 0;
            }

            if (reader.TryPeek(out var type)
                && TryGetBufferLength(type, out var length)
                && reader.TryReadByteArray(length, out var key))
            {
                value = new EncodedPublicKey(key);
                return true;
            }

            value = default;
            return false;
        }

        public void Write(IBufferWriter<byte> writer)
        {
            var span = writer.GetSpan(Key.Length);
            Key.AsSpan().CopyTo(span);
            writer.Advance(Key.Length);
        }
    }
}
