using DevHawk.Buffers;
using NeoFx.Storage;
using System;
using System.Buffers;
using System.Collections.Generic;
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
                                     IEnumerable<TransactionAttribute> attributes,
                                     IEnumerable<CoinReference> inputs,
                                     IEnumerable<TransactionOutput> outputs,
                                     IEnumerable<Witness> witnesses)
            : base(version, attributes, inputs, outputs, witnesses)
        {
            PublicKey = publicKey;
        }

        private EnrollmentTransaction(EncodedPublicKey publicKey, byte version, in CommonData commonData)
            : base(version, commonData)
        {
            PublicKey = publicKey;
        }

        public static bool TryRead(ref BufferReader<byte> reader, byte version, [NotNullWhen(true)] out EnrollmentTransaction? tx)
        {
            if (EncodedPublicKey.TryRead(ref reader, out var publicKey)
                && TryReadCommonData(ref reader, out var commonData))
            {
                tx = new EnrollmentTransaction(publicKey, version, commonData);
                return true;
            }

            tx = null;
            return false;
        }

        public override TransactionType GetTransactionType() => TransactionType.Enrollment;

        public override int GetTransactionDataSize() => PublicKey.Size;

        public override void WriteTransactionData(ref BufferWriter<byte> writer)
        {
            PublicKey.WriteTo(ref writer);
        }
    }
}
