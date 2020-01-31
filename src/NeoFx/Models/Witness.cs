using DevHawk.Buffers;
using NeoFx.Storage;
using System.Collections.Immutable;

namespace NeoFx.Models
{
    public readonly struct Witness
    {
        public readonly struct Factory : IFactoryReader<Witness>
        {
            public bool TryReadItem(ref BufferReader<byte> reader, out Witness value) => Witness.TryRead(ref reader, out value);
        }

        public readonly ImmutableArray<byte> InvocationScript;
        public readonly ImmutableArray<byte> VerificationScript;

        public int Size => InvocationScript.GetVarSize() + VerificationScript.GetVarSize();

        public Witness(ImmutableArray<byte> invocationScript, ImmutableArray<byte> verificationScript)
        {
            InvocationScript = invocationScript;
            VerificationScript = verificationScript;
        }

        public static bool TryRead(ref BufferReader<byte> reader, out Witness value)
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
