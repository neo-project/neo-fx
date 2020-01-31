using FluentAssertions;
using NeoFx;
using System;
using System.Linq;
using Xunit;

namespace NeoFxTests
{
    public class ContractTests
    {
        [Fact]
        public void Test_CreateMultiSigRedeemScript()
        {
            var neoValidators = Utility.GetNeoValidators();
            var expected = Neo.SmartContract.Contract.CreateMultiSigRedeemScript(
                (neoValidators.Length / 2) + 1, neoValidators);

            var fxValidators = Utility.GetNeoFxValidators();
            var actual = Contract.CreateMultiSigRedeemScript(fxValidators, (fxValidators.Length / 2) + 1);

            actual.Span.SequenceEqual(expected).Should().BeTrue();
        }

    }
}
