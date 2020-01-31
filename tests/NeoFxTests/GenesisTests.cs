using FluentAssertions;
using NeoFx;
using Xunit;

namespace NeoFxTests
{
    public class GenesisTests
    {
        [Fact]
        public void Test_NEO_hash()
        {
            var expected = new UInt256(Neo.Ledger.Blockchain.GoverningToken.Hash.ToArray());

            var neoTx = Genesis.CreateGoverningTokenTx();
            var actual = neoTx.CalculateHash();
            actual.Should().Be(expected);
        }

        [Fact]
        public void Test_GAS_hash()
        {
            var expected = new UInt256(Neo.Ledger.Blockchain.UtilityToken.Hash.ToArray());

            var gasTx = Genesis.CreateUtilityTokenTx();
            var actual = gasTx.CalculateHash();
            actual.Should().Be(expected);
        }

        [Fact]
        public void Test_genesis_issue_hash()
        {
            var neoIssueTx = Neo.Ledger.Blockchain.GenesisBlock.Transactions[3];
            (neoIssueTx is Neo.Network.P2P.Payloads.IssueTransaction).Should().BeTrue();
            var expected = new UInt256(neoIssueTx.Hash.ToArray());

            var fxValidators = Utility.GetNeoFxValidators();
            var fxNeoTx = Genesis.CreateGoverningTokenTx();
            var fxIssueTx = Genesis.CreateIssueTx(fxNeoTx, fxValidators);

            var actual = fxIssueTx.CalculateHash();
            actual.Should().Be(expected);
        }

        [Fact]
        public void Test_genesis_merkle()
        {
            var neoGenesis = Neo.Ledger.Blockchain.GenesisBlock;
            var expected = new UInt256(neoGenesis.MerkleRoot.ToArray());

            var fxValidators = Utility.GetNeoFxValidators();
            var fxGenesis = Genesis.CreateGenesisBlock(fxValidators);

            fxGenesis.MerkleRoot.Should().Be(expected);
        }

        [Fact]
        public void Test_genesis_next_consensus()
        {
            var neoGenesis = Neo.Ledger.Blockchain.GenesisBlock;
            var expected = new UInt160(neoGenesis.NextConsensus.ToArray());

            var fxValidators = Utility.GetNeoFxValidators();
            var fxGenesis = Genesis.CreateGenesisBlock(fxValidators);

            fxGenesis.NextConsensus.Should().Be(expected);
        }

        [Fact]
        public void Test_genesis_block_hash()
        {
            var neoGenesis = Neo.Ledger.Blockchain.GenesisBlock;
            var expected = new UInt256(neoGenesis.Hash.ToArray());

            var fxValidators = Utility.GetNeoFxValidators();
            var fxGenesis = Genesis.CreateGenesisBlock(fxValidators);
            var actual = fxGenesis.CalculateHash();
            actual.Should().Be(expected);
        }
    }
}
