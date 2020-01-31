using FluentAssertions;
using NeoFx;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using Xunit;

namespace NeoFxTests
{
    public static class Utility
    {
        [Fact]
        public static void test_validator_conversion()
        {
            var neoValidators = GetNeoValidators();
            var fxValidators = GetNeoFxValidators();

            Assert.Equal(neoValidators.Length, fxValidators.Length);

            for (int i = 0; i < fxValidators.Length; i++)
            {
                var neo = neoValidators[i];
                var fx = fxValidators[i];

                var neoBytes = neo.EncodePoint(false);
                Assert.True(fx.TryEncodePoint(false, out var fxBytes));
                Assert.True(neoBytes.AsSpan().SequenceEqual(fxBytes.AsSpan()));
            }
        }

        public static Neo.Cryptography.ECC.ECPoint[] GetNeoValidators()
        {
            return Neo.ProtocolSettings.Default.StandbyValidators
                .Where(f => f != null)
                .Select(Neo.Helper.HexToBytes)
                .Select(p => Neo.Cryptography.ECC.ECPoint.DecodePoint(p,
                    Neo.Cryptography.ECC.ECCurve.Secp256r1))
                .ToArray();
        }

        public static ECPoint[] GetNeoFxValidators()
        {
            static ECPoint DecodeValidator(string validator, in ECCurve curve)
            {
                var bytes = Neo.Helper.HexToBytes(validator);
                var immutableBytes = ImmutableArray.Create(bytes);
                var key = new EncodedPublicKey(immutableBytes);
                if (!key.TryDecode(curve, out var point))
                {
                    throw new ArgumentException(nameof(validator));
                }
                return point;
            }

            var curve = ECCurve.NamedCurves.nistP256.GetExplicit();
            return Neo.ProtocolSettings.Default.StandbyValidators
                .Select(v => DecodeValidator(v, curve))
                .ToArray();
        }
    }
}
