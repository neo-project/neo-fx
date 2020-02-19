using DevHawk.Buffers;
using NeoFx.Storage;
using System;
using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace NeoFx
{
    // TODO: this type should hold either a compressed or uncompressed public key in 
    //       a fixed size buffer
    public readonly struct EncodedPublicKey : IWritable<EncodedPublicKey>
    {
        public static readonly EncodedPublicKey Infinity
            = new EncodedPublicKey(ImmutableArray.Create<byte>(0));

        public readonly ImmutableArray<byte> Key;

        public int Size => Key.Length;

        public EncodedPublicKey(ImmutableArray<byte> key)
        {
            if (key == default)
            {
                this = Infinity;
            }
            else
            {
                Key = key;
            }
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

        public static EncodedPublicKey Encode(ECPoint point, bool compressed)
        {
            if (TryEncode(point, compressed, out var value))
            {
                return value;
            }

            throw new ArgumentException(nameof(point));
        }

        public bool TryCompress(out EncodedPublicKey value)
        {
            if (Key.Length == 1 && Key[0] == 0x00)
            {
                value = this;
                return true;
            }

            if (Key.Length == 33
                && (Key[0] == 0x02
                || Key[0] == 0x03))
            {
                value = this;
                return true;
            }

            if (Key.Length == 65
                && (Key[0] == 0x04
                || Key[0] == 0x06
                || Key[0] == 0x07))
            {

                var newKey = new byte[33];
                Key.CopyTo(newKey);
                var y = new BigInteger(Key.AsSpan().Slice(33, 32), true, true);
                newKey[0] = y.IsEven ? (byte)0x02 : (byte)0x03;

                var immutableNewKey = Unsafe.As<byte[], ImmutableArray<byte>>(ref newKey);
                value = new EncodedPublicKey(immutableNewKey);
                return true;
            }

            value = default;
            return false;
        }

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

        public void WriteTo(ref BufferWriter<byte> writer)
        {
            writer.Write(Key.AsSpan());
        }
    }
}
