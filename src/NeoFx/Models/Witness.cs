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
    }
}
