using NeoFx.Models;
using System;

namespace NeoFx
{
    public static class MerkleHash
    {
        public static bool TryCompute(in ReadOnlySpan<UInt256> hashes, out UInt256 merkleHash)
        {
            static bool TryWriteLeaf(in UInt256 hash, Span<byte> buffer)
            {
                return hash.TryWrite(buffer);
            }

            if (hashes.Length == 0)
            {
                merkleHash = default;
                return false;
            }

            if (hashes.Length == 1)
            {
                merkleHash = hashes[0];
                return true;
            }

            Span<UInt256> parents = stackalloc UInt256[(hashes.Length + 1) / 2];
            Span<byte> buffer = stackalloc byte[UInt256.Size * 2];
            Span<byte> hash = stackalloc byte[UInt256.Size];

            for (var i = 0; i < parents.Length; i++)
            {
                var l = i * 2;
                var l2 = l + 1;

                if (hashes[l].TryWrite(buffer.Slice(0, UInt256.Size))
                    && TryWriteLeaf(
                        l2 == hashes.Length ? hashes[l] : hashes[l2],
                        buffer.Slice(UInt256.Size, UInt256.Size))
                    && HashHelpers.TryHash256(buffer, hash))
                {
                    parents[i] = new UInt256(hash);
                }
                else
                {
                    merkleHash = default;
                    return false;
                }
            }

            return TryCompute(parents, out merkleHash);
        }

        public static bool TryCompute(ReadOnlySpan<Transaction> transactions, out UInt256 merkleHash)
        {
            Span<UInt256> hashes = stackalloc UInt256[transactions.Length];
            for (int i = 0; i < hashes.Length; i++)
            {
                hashes[i] = transactions[i].CalculateHash();
            }

            return TryCompute(hashes, out merkleHash);
        }

        public static UInt256 Compute(ReadOnlySpan<Transaction> transactions)
        {
            if (TryCompute(transactions, out var merkleHash))
            {
                return merkleHash;
            }

            throw new ArgumentException(nameof(transactions));
        }
    }
}
