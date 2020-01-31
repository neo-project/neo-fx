using NeoFx.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;

namespace NeoFx
{
    public static class Genesis
    {
        public static RegisterTransaction CreateGoverningTokenTx()
        {
            ReadOnlySpan<byte> adminScript = stackalloc byte[] { OpCode.PUSHT };
            var adminScriptHash = adminScript.CalculateScriptHash();

            return new RegisterTransaction(
                assetType: AssetType.GoverningToken,
                name: "[{\"lang\":\"zh-CN\",\"name\":\"小蚁股\"},{\"lang\":\"en\",\"name\":\"AntShare\"}]",
                amount: Fixed8.Create((decimal)100000000),
                precision: 0,
                owner: EncodedPublicKey.Infinity,
                admin: adminScriptHash,
                version: 0);
        }

        public static RegisterTransaction CreateUtilityTokenTx()
        {
            const uint DecrementInterval = 2000000;
            ReadOnlySpan<uint> GenerationAmount = stackalloc uint[] { 8, 7, 6, 5, 4, 3, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };

            uint generationTotal = 0;
            for (var i = 0; i < GenerationAmount.Length; i++)
            {
                generationTotal += GenerationAmount[i];
            }
            var amount = Fixed8.Create((decimal)(generationTotal * DecrementInterval));

            ReadOnlySpan<byte> adminScript = stackalloc byte[] { OpCode.PUSHF };
            var adminScriptHash = adminScript.CalculateScriptHash();

            return new RegisterTransaction(
                assetType: AssetType.UtilityToken,
                name: "[{\"lang\":\"zh-CN\",\"name\":\"小蚁币\"},{\"lang\":\"en\",\"name\":\"AntCoin\"}]",
                amount: amount,
                precision: 8,
                owner: EncodedPublicKey.Infinity,
                admin: adminScriptHash,
                version: 0);
        }

        public static IssueTransaction CreateIssueTx(RegisterTransaction governingTx, IEnumerable<ECPoint> validators)
        {
            var governingTxHash = governingTx.CalculateHash();

            var validatorCount = (validators.Count() / 2) + 1;
            var validatorScript = Contract.CreateMultiSigRedeemScript(validators, validatorCount);
            var validatorScriptHash = validatorScript.Span.CalculateScriptHash();
            var verificationScript = ImmutableArray.Create<byte>(OpCode.PUSHT);

            return new IssueTransaction(
                version: 0,
                outputs: new[]
                {
                    new TransactionOutput(governingTxHash, governingTx.Amount, validatorScriptHash)
                },
                witnesses: new[] { new Witness(default, verificationScript) });
        }

        public static Block CreateGenesisBlock(IEnumerable<ECPoint> validators)
        {
            var minerTx = new MinerTransaction(2083236893, 0);
            var neoTx = CreateGoverningTokenTx();
            var gasTx = CreateUtilityTokenTx();
            var issueTx = CreateIssueTx(neoTx, validators);

            var transactions = ImmutableArray.Create<Transaction>(minerTx,
                                                                  neoTx,
                                                                  gasTx,
                                                                  issueTx);

            var validatorCount = validators.Count();
            var consensusCount = validatorCount - (validatorCount - 1) / 3;
            var validatorScript = Contract.CreateMultiSigRedeemScript(validators, consensusCount);
            var validatorScriptHash = validatorScript.Span.CalculateScriptHash();
            var merkleHash = MerkleHash.Compute(transactions.AsSpan());
            var verificationScript = ImmutableArray.Create<byte>(OpCode.PUSHT);

            var header = new BlockHeader(
                version: 0,
                previousHash: UInt256.Zero,
                merkleRoot: merkleHash,
                timestamp: new DateTimeOffset(2016, 7, 15, 15, 8, 21, TimeSpan.Zero),
                index: 0,
                consensusData: 2083236893, // Tribute to Bitcoin
                nextConsensus: validatorScriptHash,
                witness: new Witness(default, verificationScript));

            return new Block(header, transactions);
        }
    }
}
