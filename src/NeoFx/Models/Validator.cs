using DevHawk.Buffers;

namespace NeoFx.Models
{
    public readonly struct Validator
    {
        public readonly EncodedPublicKey PublicKey;
        public readonly bool Registered;
        public readonly Fixed8 Votes;

        public Validator(EncodedPublicKey publicKey, bool registered, Fixed8 votes)
        {
            PublicKey = publicKey;
            Registered = registered;
            Votes = votes;
        }

        public static bool TryRead(ref BufferReader<byte> reader, out Validator value)
        {
            if (EncodedPublicKey.TryRead(ref reader, out var publicKey)
                && reader.TryRead(out byte registered)
                && Fixed8.TryRead(ref reader, out var votes))
            {
                value = new Validator(publicKey, registered != 0, votes);
                return true;
            }
            value = default;
            return false;
        }
    }
}
