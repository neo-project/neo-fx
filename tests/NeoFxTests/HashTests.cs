using FluentAssertions;
using NeoFx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace NeoFxTests
{
    public class HashTests
    {
        private static Lazy<SHA256> _sha256 = new Lazy<SHA256>(() => SHA256.Create());

        [Fact]
        public void Test_neofx_Base58CheckDecode_matches_neo()
        {
            var @string = "AXaXZjZGA3qhQRTCsyG5uFKr9HeShgVhTF";
            var expected = Neo.Cryptography.Helper.Base58CheckDecode(@string);

            Span<byte> actual = stackalloc byte[HashHelpers.GetBase58CheckDecodeByteCount(@string)];
            HashHelpers.TryBase58CheckDecode(@string, actual, out var written).Should().BeTrue();
            written.Should().Be(actual.Length);
            actual.SequenceEqual(expected).Should().BeTrue();
        }

        [Fact]
        public void Test_interop_method_hash_matches_neo()
        {
            var @string = "Neo.Runtime.GetTrigger";
            var expected = Neo.SmartContract.Helper.ToInteropMethodHash(@string);

            HashHelpers.TryInteropMethodHash(@string, out var actual).Should().BeTrue();
            actual.Should().Be(expected);
        }
    }
}
