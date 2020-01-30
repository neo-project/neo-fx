using System;

namespace NeoFx.TestNode
{
    public static class Helpers
    {
        public static bool TryConvertHexString(this string hex, Span<byte> span, out int bytesWritten)
        {
            static int GetHexVal(char hex)
            {
                return (int)hex - ((int)hex < 58 ? 48 : ((int)hex < 97 ? 55 : 87));
            }

            if (hex.Length % 2 == 0
                && span.Length >= hex.Length >> 1)
            {
                for (int i = 0; i < hex.Length >> 1; ++i)
                {
                    span[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
                }

                bytesWritten = hex.Length >> 1;
                return true;
            }

            bytesWritten = default;
            return false;
        }
    }
}
