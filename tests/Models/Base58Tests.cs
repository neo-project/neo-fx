using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace NeoFx.Models.Tests
{
    public class Base58Tests
    {
        private static Lazy<SHA256> _sha256 = new Lazy<SHA256>(() => SHA256.Create());

        [Fact]
        public void Test_neofx_Base58CheckDecode_matches_neo()
        {
            var @string = "AXaXZjZGA3qhQRTCsyG5uFKr9HeShgVhTF";
            var expected = Neo.Cryptography.Helper.Base58CheckDecode(@string);
            var actual = Utility.Base58CheckDecode(@string);

            actual.AsSpan().SequenceEqual(expected).Should().BeTrue();
        }
    }
}
