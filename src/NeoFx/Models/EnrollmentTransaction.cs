using NeoFx.Storage;
using System;
using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NeoFx.Models
{
    [Obsolete]
    public sealed class EnrollmentTransaction : Transaction
    {
        public readonly EncodedPublicKey PublicKey;

        public EnrollmentTransaction(EncodedPublicKey publicKey,
                                     byte version,
                                     ImmutableArray<TransactionAttribute> attributes,
                                     ImmutableArray<CoinReference> inputs,
                                     ImmutableArray<TransactionOutput> outputs,
                                     ImmutableArray<Witness> witnesses)
            : base(version, attributes, inputs, outputs, witnesses)
        {
            PublicKey = publicKey;
        }
        public static bool TryRead(ref SpanReader<byte> reader, byte version, [NotNullWhen(true)] out EnrollmentTransaction? tx)
        {
            if (EncodedPublicKey.TryRead(ref reader, out var publicKey)
                && reader.TryReadVarArray<TransactionAttribute>(TransactionAttribute.TryRead, out var attributes)
                && reader.TryReadVarArray<CoinReference>(CoinReference.TryRead, out var inputs)
                && reader.TryReadVarArray<TransactionOutput>(TransactionOutput.TryRead, out var outputs)
                && reader.TryReadVarArray<Witness>(Witness.TryRead, out var witnesses))
            {
                tx = new EnrollmentTransaction(publicKey, version, attributes, inputs, outputs, witnesses);
                return true;
            }

            tx = null;
            return false;
        }

        public override void WriteTransactionData(IBufferWriter<byte> writer)
        {
            writer.WriteLittleEndian((byte)TransactionType.Enrollment);
            writer.WriteLittleEndian(Version);
            writer.Write(PublicKey);
        }
    }
}
