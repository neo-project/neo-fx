using System;
using System.Diagnostics;

namespace NeoFx
{
    public static class Utility
    {
        public static bool TryConvertHexString(this string hex, Span<byte> span, out int bytesWritten)
        {
            return TryConvertHexString(hex.AsSpan(), span, out bytesWritten);
        }

        // TODO: investigate https://www.codeproject.com/tips/447938/high-performance-csharp-byte-array-to-hex-string-t
        public static bool TryConvertHexString(this ReadOnlySpan<char> hex, Span<byte> span, out int bytesWritten)
        {
            if (hex.Length % 2 == 0
                && span.Length >= hex.Length >> 1)
            {
                for (int i = 0; i < hex.Length >> 1; ++i)
                {
                    if (byte.TryParse(hex.Slice(i << 1, 2), System.Globalization.NumberStyles.AllowHexSpecifier, null, out var result))
                    {
                        span[i] = result;
                    }
                    else
                    {
                        bytesWritten = default;
                        return false;
                    }
                }

                bytesWritten = hex.Length >> 1;
                return true;
            }

            bytesWritten = default;
            return false;
        }

        public static bool TryHexFormat(this ReadOnlySpan<byte> src, Span<char> destination, out int charsWritten)
        {
            charsWritten = default;

            if (destination.Length >= src.Length * 2)
            {
                for (int i = 0; i < src.Length; i++)
                {
                    if (!src[i].TryFormat(destination.Slice(i * 2, 2), out var written, "x2"))
                    {
                        return false;
                    }
                    Debug.Assert(written == 2);
                }

                charsWritten = src.Length * 2;
                return true;
            }

            return false;
        }

    }
}
