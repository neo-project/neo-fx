using NeoFx.Storage;
using System.Collections.Immutable;

namespace NeoFx.Models
{
    public readonly struct Witness
    {
        public readonly ImmutableArray<byte> InvocationScript;
        public readonly ImmutableArray<byte> VerificationScript;

        public Witness(ImmutableArray<byte> invocationScript, ImmutableArray<byte> verificationScript)
        {
            InvocationScript = invocationScript;
            VerificationScript = verificationScript;
        }

        public static bool TryRead(ref SpanReader<byte> reader, out Witness value)
        {
            if (reader.TryReadVarArray(65536, out var invocation)
                && reader.TryReadVarArray(65536, out var verification))
            {
                value = new Witness(invocation, verification);
                return true;
            }

            value = default;
            return false;
        }

    }
}
