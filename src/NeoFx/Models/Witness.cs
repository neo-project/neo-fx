using DevHawk.Buffers;
using NeoFx.Storage;
using System.Collections.Immutable;
using System.Diagnostics;

namespace NeoFx.Models
{
    public readonly struct Witness : IWritable<Witness>
    {
        public readonly struct Factory : IFactoryReader<Witness>
        {
            public bool TryReadItem(ref BufferReader<byte> reader, out Witness value) => Witness.TryRead(ref reader, out value);
        }

        const uint MAX_SCRIPT_LENGTH = 65536;

        public readonly ImmutableArray<byte> InvocationScript;
        public readonly ImmutableArray<byte> VerificationScript;

        public int Size => InvocationScript.GetVarSize() + VerificationScript.GetVarSize();

        public Witness(ImmutableArray<byte> invocationScript, ImmutableArray<byte> verificationScript)
        {
            Debug.Assert(invocationScript.Length <= MAX_SCRIPT_LENGTH);
            Debug.Assert(verificationScript.Length <= MAX_SCRIPT_LENGTH);

            InvocationScript = invocationScript;
            VerificationScript = verificationScript;
        }

        public static bool TryRead(ref BufferReader<byte> reader, out Witness value)
        {
            if (reader.TryReadVarArray(MAX_SCRIPT_LENGTH, out var invocation)
                && reader.TryReadVarArray(MAX_SCRIPT_LENGTH, out var verification))
            {
                value = new Witness(invocation, verification);
                return true;
            }

            value = default;
            return false;
        }

        public void WriteTo(ref BufferWriter<byte> writer)
        {
            writer.WriteVarArray(InvocationScript);
            writer.WriteVarArray(VerificationScript);
        }
    }
}
