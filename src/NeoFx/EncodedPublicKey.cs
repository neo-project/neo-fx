using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace NeoFx
{
    public readonly struct EncodedPublicKey
    {
        public readonly ReadOnlyMemory<byte> Key;

        public EncodedPublicKey(ReadOnlyMemory<byte> key)
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
            return Utility.TryDecodePoint(Key.Span, curve, out point);
        }

        public static bool TryEncode(ECPoint point, bool compressed, out EncodedPublicKey value)
        {
            var buffer = new byte[65];
            if (Utility.TryEncodePoint(point, buffer, compressed, out var written))
            {
                Debug.Assert(written < buffer.Length);
                value = new EncodedPublicKey(buffer.AsMemory().Slice(0, written));
                return true;
            }

            value = default;
            return false;
        }
    }
}
