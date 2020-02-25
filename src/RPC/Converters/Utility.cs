using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using DevHawk.Buffers;
using NeoFx.Storage;
using Newtonsoft.Json;

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

        public static bool TryReadHexToken<T>(this JsonReader reader, TryReadItem<T> factory, [MaybeNullWhen(false)] out T value)
        {
            if (reader.TokenType == JsonToken.String)
            {
                var hex = (string)reader.Value; 

                using var memoryOwner = MemoryPool<byte>.Shared.Rent(hex.Length >> 1);
                if (hex.TryConvertHexString(memoryOwner.Memory.Span, out var bytesWritten))
                {
                    var bufferReader = new BufferReader<byte>(memoryOwner.Memory.Span.Slice(0, hex.Length >> 1));
                    if (factory(ref bufferReader, out value))
                    {
                        return true;
                    }
                }
            }

            value = default!;
            return false;
        }
    }
}
