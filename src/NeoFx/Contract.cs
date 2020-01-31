using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using DevHawk.Buffers;

namespace NeoFx
{
    public static class Contract
    {
        private class ECPointComparer : IComparer<ECPoint>
        {
            public int Compare(ECPoint l, ECPoint r) => ECPointHelpers.Compare(l, r);
        }

        public static ReadOnlyMemory<byte> CreateMultiSigRedeemScript(IEnumerable<ECPoint> publicKeys, int count)
        {
            var comparer = new ECPointComparer();

            var buffer = new ArrayBufferWriter<byte>();
            var writer = new BufferWriter<byte>(buffer);

            writer.EmitPush(count);

            foreach (var key in publicKeys.OrderBy(pk => pk, comparer))
            {
                if (!EncodedPublicKey.TryEncode(key, true, out var encodedKey))
                    throw new ArgumentException(nameof(publicKeys));

                writer.EmitPush(encodedKey.Key.AsSpan());
            }
            writer.EmitPush(publicKeys.Count());
            writer.EmitOpCode(OpCode.CHECKMULTISIG);
            writer.Commit();

            return buffer.WrittenMemory;
        }

        // public static ReadOnlyMemory<byte> CreateSignatureRedeemScript(ECPoint publicKey)
        // {
        //     if (!EncodedPublicKey.TryEncode(publicKey, true, out var encodedKey))
        //         throw new ArgumentException(nameof(publicKey));

        //     var buffer = new ArrayBufferWriter<byte>();
        //     buffer.EmitPush(encodedKey.Key.Span);
        //     buffer.EmitOpCode(OpCode.CHECKSIG);
        //     return buffer.WrittenMemory;
        // }
    }
}
