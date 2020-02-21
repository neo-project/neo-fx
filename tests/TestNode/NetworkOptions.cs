using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using NeoFx.Models;

namespace NeoFx.TestNode
{
    public class NetworkOptions
    {
        public uint Magic { get; set; }
        public string[] Seeds { get; set; } = Array.Empty<string>();
        public string[] Validators { get; set; } = Array.Empty<string>();

        public static IEnumerable<ECPoint> ConvertValidators(string[] validators)
        {
            static bool TryConvertHexString(string hex, out ImmutableArray<byte> value)
            {
                static int GetHexVal(char hex)
                {
                    return (int)hex - ((int)hex < 58 ? 48 : ((int)hex < 97 ? 55 : 87));
                }

                if (hex.Length % 2 == 0)
                {
                    var bytesLength = hex.Length >> 1;
                    var array = new byte[bytesLength];

                    for (int i = 0; i < bytesLength; ++i)
                    {
                        var charIndex = i << 1;
                        array[i] = (byte)((GetHexVal(hex[charIndex]) << 4) + (GetHexVal(hex[charIndex + 1])));
                    }

                    value = Unsafe.As<byte[], ImmutableArray<byte>>(ref array);
                    return true;
                }

                value = default;
                return false;
            }

            var curve = ECCurve.NamedCurves.nistP256.GetExplicit();

            for (int i = 0; i < validators.Length; i++)
            {
                if (TryConvertHexString(validators[i], out var bytes)
                    && (new EncodedPublicKey(bytes)).TryDecode(curve, out var point))
                {
                    yield return point;
                }
            }
        }

    }
}
