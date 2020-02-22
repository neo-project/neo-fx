using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace NeoFx
{
    public static class ECPointHelpers
    {
        public static int Compare(ECPoint l, ECPoint r)
        {
            static int CompareByteArray(byte[] left, byte[] right)
            {
                var bigIntLeft = new BigInteger(left, isUnsigned: true, isBigEndian: true);
                var bigIntRight = new BigInteger(right, isUnsigned: true, isBigEndian: true);
                return bigIntLeft.CompareTo(bigIntRight);
            }

            int result = CompareByteArray(l.X, r.X);
            if (result != 0) return result;
            return CompareByteArray(l.Y, r.Y);
        }

        public static ECCurve GetExplicit(this ECCurve curve)
        {
            if (!curve.IsExplicit && curve.IsNamed)
            {
                using var ecdsa = ECDsa.Create(curve);
                return ecdsa.ExportExplicitParameters(false).Curve;
            }

            return curve;
        }

        internal static bool TryDecodePoint(this ECCurve curve, ReadOnlySpan<byte> encodedPoint, out ECPoint point)
        {
            curve = GetExplicit(curve);

            var prime = new BigInteger(curve.Prime, isUnsigned: true, isBigEndian: true);
            int expectedLength = (prime.GetBitLength() + 7) / 8;

            switch (encodedPoint[0])
            {
                case 0x00:
                    point = new ECPoint();
                    return true;
                case 0x02:
                case 0x03:
                    Debug.Assert(encodedPoint.Length == expectedLength + 1);
                    int yTilde = encodedPoint[0] & 1;
                    return TryDecompressPoint(encodedPoint.Slice(1), yTilde, expectedLength, curve, out point);
                case 0x04:
                case 0x06:
                case 0x07:
                    Debug.Assert(encodedPoint.Length == (2 * expectedLength) + 1);
                    point = new ECPoint()
                    {
                        X = encodedPoint.Slice(1, expectedLength).ToArray(),
                        Y = encodedPoint.Slice(1 + expectedLength, expectedLength).ToArray()
                    };
                    return true;
                default:
                    point = default;
                    return false;
            }
        }

        internal static bool TryEncodePoint(this ECPoint point, bool compressed, out ImmutableArray<byte> encodedPoint)
        {
            if (point.X == null
                && point.Y == null)
            {
                encodedPoint = ImmutableArray.Create<byte>(0);
                return true;
            }
            else if (compressed)
            {
                var array = new byte[33];
                if (point.X.AsSpan().TryCopyTo(array.AsSpan().Slice(1, 32)))
                {
                    var y = new BigInteger(point.Y, true, true);
                    array[0] = y.IsEven ? (byte)0x02 : (byte)0x03;
                    encodedPoint = Unsafe.As<byte[], ImmutableArray<byte>>(ref array);
                    return true;
                }

            }
            else
            {
                var array = new byte[65];
                if (point.X.AsSpan().TryCopyTo(array.AsSpan().Slice(1, 32))
                    && point.Y.AsSpan().TryCopyTo(array.AsSpan().Slice(33, 32)))
                {
                    array[0] = 0x04;
                    encodedPoint = Unsafe.As<byte[], ImmutableArray<byte>>(ref array);
                    return true;
                }
            }

            encodedPoint = default;
            return false;
        }

        private static bool TryDecompressPoint(ReadOnlySpan<byte> xSpan, int yTilde, int expectedLength, ECCurve curve, out ECPoint point)
        {
            Debug.Assert(curve.IsExplicit);

            var x = new BigInteger(xSpan, isUnsigned: true, isBigEndian: true);
            var a = new BigInteger(curve.A, isUnsigned: true, isBigEndian: true);
            var b = new BigInteger(curve.B, isUnsigned: true, isBigEndian: true);
            var prime = new BigInteger(curve.Prime, isUnsigned: true, isBigEndian: true);

            var alpha = Mod((x * ((x * x) + a)) + b, prime);
            if (TrySqrt(alpha, prime, out var beta))
            {
                if ((beta.IsEven ? 0 : 1) != yTilde)
                {
                    beta = prime - beta;
                }

                if (x.TryToByteArray(expectedLength, out var xArray)
                    && beta.TryToByteArray(expectedLength, out var betaArray))
                {
                    point = new ECPoint()
                    {
                        X = xArray,
                        Y = betaArray
                    };
                    return true;
                }
            }

            point = default;
            return false;
        }

        private static bool TestBit(this BigInteger bigInteger, int index)
        {
            return (bigInteger & (BigInteger.One << index)) > BigInteger.Zero;
        }

        private static BigInteger Mod(this BigInteger x, BigInteger y)
        {
            x %= y;
            if (x.Sign < 0)
                x += y;
            return x;
        }

        private static int GetBitLength(this BigInteger bigInt)
        {
            Span<byte> buffer = stackalloc byte[bigInt.GetByteCount()];
            if (bigInt.TryWriteBytes(buffer, out var written))
            {
                Debug.Assert(written <= buffer.Length);
                return GetBigIntegerBitLength(buffer, bigInt.Sign);
            }

            throw new ArgumentException(nameof(bigInt));
        }

        private static int GetBigIntegerBitLength(Span<byte> b, int sign)
        {
            static int BitLen(int w)
            {
                return (w < 1 << 15 ? (w < 1 << 7
                    ? (w < 1 << 3 ? (w < 1 << 1
                    ? (w < 1 << 0 ? (w < 0 ? 32 : 0) : 1)
                    : (w < 1 << 2 ? 2 : 3)) : (w < 1 << 5
                    ? (w < 1 << 4 ? 4 : 5)
                    : (w < 1 << 6 ? 6 : 7)))
                    : (w < 1 << 11
                    ? (w < 1 << 9 ? (w < 1 << 8 ? 8 : 9) : (w < 1 << 10 ? 10 : 11))
                    : (w < 1 << 13 ? (w < 1 << 12 ? 12 : 13) : (w < 1 << 14 ? 14 : 15)))) : (w < 1 << 23 ? (w < 1 << 19
                    ? (w < 1 << 17 ? (w < 1 << 16 ? 16 : 17) : (w < 1 << 18 ? 18 : 19))
                    : (w < 1 << 21 ? (w < 1 << 20 ? 20 : 21) : (w < 1 << 22 ? 22 : 23))) : (w < 1 << 27
                    ? (w < 1 << 25 ? (w < 1 << 24 ? 24 : 25) : (w < 1 << 26 ? 26 : 27))
                    : (w < 1 << 29 ? (w < 1 << 28 ? 28 : 29) : (w < 1 << 30 ? 30 : 31)))));
            }

            return ((b.Length - 1) * 8) + BitLen(sign > 0 ? b[b.Length - 1] : 255 - b[b.Length - 1]);
        }

        private static (BigInteger U, BigInteger V) FastLucasSequence(BigInteger p, BigInteger P, BigInteger Q, BigInteger k)
        {
            static (int bitLength, int lowestBitSet) GetBitLengthAndLowestSetBit(BigInteger bigInt)
            {
                Span<byte> buffer = stackalloc byte[bigInt.GetByteCount()];
                if (bigInt.TryWriteBytes(buffer, out var written))
                {
                    Debug.Assert(written <= buffer.Length);
                    var bitLength = GetBigIntegerBitLength(buffer, bigInt.Sign);

                    if (bigInt.Sign == 0)
                    {
                        return (bitLength, -1);
                    }

                    int w = 0;
                    while (buffer[w] == 0)
                    {
                        w++;
                    }

                    for (int x = 0; x < 8; x++)
                    {
                        if ((buffer[w] & 1 << x) > 0)
                        {
                            return (bitLength, x + (w * 8));
                        }
                    }
                }

                throw new ArgumentException(nameof(bigInt));
            }

            var (n, s) = GetBitLengthAndLowestSetBit(k);

            Debug.Assert(k.TestBit(s));

            BigInteger Uh = 1;
            BigInteger Vl = 2;
            BigInteger Vh = P;
            BigInteger Ql = 1;
            BigInteger Qh = 1;

            for (int j = n - 1; j >= s + 1; --j)
            {
                Ql = (Ql * Qh).Mod(p);

                if (k.TestBit(j))
                {
                    Qh = (Ql * Q).Mod(p);
                    Uh = (Uh * Vh).Mod(p);
                    Vl = ((Vh * Vl) - (P * Ql)).Mod(p);
                    Vh = ((Vh * Vh) - (Qh << 1)).Mod(p);
                }
                else
                {
                    Qh = Ql;
                    Uh = (Uh * Vl - Ql).Mod(p);
                    Vh = (Vh * Vl - (P * Ql)).Mod(p);
                    Vl = ((Vl * Vl) - (Ql << 1)).Mod(p);
                }
            }

            Ql = (Ql * Qh).Mod(p);
            Qh = (Ql * Q).Mod(p);
            Uh = ((Uh * Vl) - Ql).Mod(p);
            Vl = ((Vh * Vl) - (P * Ql)).Mod(p);
            Ql = (Ql * Qh).Mod(p);

            for (int j = 1; j <= s; ++j)
            {
                Uh = Uh * Vl * p;
                Vl = ((Vl * Vl) - (Ql << 1)).Mod(p);
                Ql = (Ql * Ql).Mod(p);
            }

            return (Uh, Vl);
        }

        private static BigInteger NextBigInteger(int sizeInBits)
        {
            if (sizeInBits < 0)
                throw new ArgumentException("sizeInBits must be non-negative");
            if (sizeInBits == 0)
                return 0;
            Span<byte> span = stackalloc byte[(sizeInBits / 8) + 1];
            StaticRandom.NextBytes(span);
            if (sizeInBits % 8 == 0)
                span[span.Length - 1] = 0;
            else
                span[span.Length - 1] &= (byte)((1 << sizeInBits % 8) - 1);
            return new BigInteger(span);
        }

        private static bool TrySqrt(BigInteger value, BigInteger q, out BigInteger result)
        {
            static bool TryCheckSqrt(BigInteger z, BigInteger q, BigInteger value, out BigInteger result)
            {
                if (Mod(z * z, q) == value)
                {
                    result = z;
                    return true;
                }
                else
                {
                    result = default;
                    return false;
                }
            }

            if (q.TestBit(1))
            {
                var sqrt = BigInteger.ModPow(value, (q >> 2) + BigInteger.One, q);
                return TryCheckSqrt(sqrt, q, value, out result);
            }

            BigInteger qMinusOne = q - BigInteger.One;
            BigInteger legendreExponent = qMinusOne >> 1;
            if (BigInteger.ModPow(value, legendreExponent, q) != 1)
            {
                result = default;
                return false;
            }

            BigInteger u = qMinusOne >> 2;
            BigInteger k = (u << 1) + 1;
            BigInteger Q = value;
            BigInteger fourQ = Mod(Q << 2, q);
            BigInteger U, V;
            var qBitLength = q.GetBitLength();

            do
            {
                BigInteger P;
                do
                {
                    P = NextBigInteger(qBitLength);
                }
                while (P >= q || BigInteger.ModPow((P * P) - fourQ, legendreExponent, q) != qMinusOne);

                (U, V) = FastLucasSequence(q, P, Q, k);
                if (Mod(V * V, q) == fourQ)
                {
                    if (V.TestBit(0))
                    {
                        V += q;
                    }
                    V >>= 1;
                    Debug.Assert((V * V).Mod(q) == value);

                    result = V;
                    return true;
                }
            }
            while (U.Equals(BigInteger.One) || U.Equals(qMinusOne));

            result = default;
            return false;
        }

        private static bool TryToByteArray(this BigInteger bigInteger, int expectedLength, [NotNullWhen(true)] out byte[]? buffer)
        {
            var array = new byte[expectedLength];
            var byteCount = bigInteger.GetByteCount(true);

            if (byteCount <= expectedLength
                && bigInteger.TryWriteBytes(array.AsSpan().Slice(expectedLength - byteCount), out var written, true, true))
            {
                Debug.Assert(written == byteCount);
                buffer = array;
                return true;
            }

            buffer = null;
            return false;
        }
    }
}
