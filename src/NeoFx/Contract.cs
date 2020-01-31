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
                var encodedKey = EncodedPublicKey.Encode(key, true);
                writer.EmitPush(encodedKey.Key.AsSpan());
            }
            writer.EmitPush(publicKeys.Count());
            writer.EmitOpCode(OpCode.CHECKMULTISIG);

            return buffer.WrittenMemory;
        }

        public static ReadOnlyMemory<byte> CreateSignatureRedeemScript(ECPoint publicKey)
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new BufferWriter<byte>(buffer);
            var encodedKey = EncodedPublicKey.Encode(publicKey, true);
            writer.EmitPush(encodedKey.Key.AsSpan());
            writer.EmitOpCode(OpCode.CHECKSIG);
            return buffer.WrittenMemory;
        }
    }
}
