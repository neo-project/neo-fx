using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

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
