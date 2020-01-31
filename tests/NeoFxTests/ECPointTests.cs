using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using NeoFx;

namespace NeoFxTests
{
    public class ECPointTests
    {
        static string[] testValues = new string[] {
            // Neo 2 mainnet standby validators
            "03b209fd4f53a7170ea4444e0cb0a6bb6a53c2bd016926989cf85f9b0fba17a70c",
            "02df48f60e8f3e01c48ff40b9b7f1310d7a8b2a193188befe1c2e3df740e895093",
            "03b8d9d5771d8f513aa0869b9cc8d50986403b78c6da36890638c3d46a5adce04a",
            "02ca0e27697b9c248f6f16e085fd0061e26f44da85b58ee835c110caa5ec3ba554",
            "024c7b7fb6c310fccf1ba33b082519d82964ea93868d676662d4a59ad548df0e7d",
            "02aaec38470f6aad0042c6e877cfd8087d2676b0f516fddd362801b9bd3936399e",
            "02486fd15702c4490a26703112a5cc1d0923fd697a33406bd5a1c00e0013b09a70",

            // Neo 2 testnet standby validators
            "0327da12b5c40200e9f65569476bbff2218da4f32548ff43b6387ec1416a231ee8",
            "026ce35b29147ad09e4afe4ec4a7319095f08198fa8babbe3c56e970b143528d22",
            "0209e7fd41dfb5c2f8dc72eb30358ac100ea8c72da18847befe06eade68cebfcb9",
            "039dafd8571a641058ccc832c5e2111ea39b09c0bde36050914384f7a48bce9bf9",
            "038dddc06ce687677a53d54f096d2591ba2302068cf123c1f2d75c2dddc5425579",
            "02d02b1873a0863cd042cc717da31cea0d7cf9db32b74d4c72c01b0011503e2e22",
            "034ff5ceeac41acf22cd5ed2da17a6df4dd8358fcb2bfb1a43208ad0feaab2746b",

            // Neo 3 testnet standby validators
            "023e9b32ea89b94d066e649b124fd50e396ee91369e8e2a6ae1b11c170d022256d",
            "03009b7540e10f2562e5fd8fac9eaec25166a58b26e412348ff5a86927bfac22a2",
            "02ba2c70f5996f357a43198705859fae2cfea13e1172962800772b3d588a9d4abd",
            "03408dcd416396f64783ac587ea1e1593c57d9fea880c8a6a1920e92a259477806",
            "02a7834be9b32e2981d157cb5bbd3acb42cfd11ea5c3b10224d7a44e98c5910f1b",
            "0214baf0ceea3a66f17e7e1e839ea25fd8bed6cd82e6bb6e68250189065f44ff01",
            "030205e9cefaea5a1dfc580af20c8d5aa2468bb0148f1a5e4605fc622c80e604ba"
        };

        static bool TryHexToBytes(ReadOnlySpan<char> hex, Span<byte> bytes)
        {
            if (bytes.Length >= hex.Length / 2)
            {
                for (int i = 0; i < hex.Length; i += 2)
                {
                    bytes[i / 2] = byte.Parse(hex.Slice(i, 2), System.Globalization.NumberStyles.HexNumber);
                }

                return true;
            }

            return false;
        }


        [Fact]
        public void CompareFxToNeo()
        {
            var buffer = new byte[testValues[0].Length / 2];
            var curve = ECCurve.NamedCurves.nistP256.GetExplicit();
            foreach (var test in testValues)
            {
                TryHexToBytes(test, buffer).Should().BeTrue();

                var neoPoint = Neo.Cryptography.ECC.ECPoint.DecodePoint(buffer, Neo.Cryptography.ECC.ECCurve.Secp256r1);
                var neoPublicKey = neoPoint.EncodePoint(false).AsSpan().Slice(1);

                curve.TryDecodePoint(buffer, out var fxPoint).Should().BeTrue();

                neoPublicKey.Slice(0, 32).SequenceEqual(fxPoint.X).Should().BeTrue();
                neoPublicKey.Slice(32, 32).SequenceEqual(fxPoint.Y).Should().BeTrue();
            }
        }

        [Fact]
        public void RoundTrip()
        {
            var buffer = new byte[testValues[0].Length / 2];
            var curve = ECCurve.NamedCurves.nistP256.GetExplicit();

            foreach (var test in testValues)
            {
                TryHexToBytes(test, buffer).Should().BeTrue();

                curve.TryDecodePoint(buffer, out var fxPoint).Should().BeTrue();
                fxPoint.TryEncodePoint(true, out var newBuffer).Should().BeTrue();
                buffer.AsSpan().SequenceEqual(newBuffer.AsSpan()).Should().BeTrue();
            }
        }
    }
}
