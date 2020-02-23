using System;
using System.Globalization;

namespace NeoFx.RPC.Converters
{
    public static class Utility
    {
        // TODO: investigate https://www.codeproject.com/tips/447938/high-performance-csharp-byte-array-to-hex-string-t
        public static bool TryConvertHexString(this string hex, Span<byte> span, out int bytesWritten)
        {
            if (hex.Length % 2 == 0
                && span.Length >= hex.Length >> 1)
            {
                var hexspan = hex.AsSpan();
                for (int i = 0; i < hex.Length >> 1; ++i)
                {
                    if (byte.TryParse(hexspan.Slice(i << 1, 2), NumberStyles.AllowHexSpecifier, null, out var result))
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
    }
}
