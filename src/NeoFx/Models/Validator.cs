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

        //public static bool TryRead(ref this SpanReader<byte> reader, out Validator value)
        //{
        //    if (reader.TryRead(out EncodedPublicKey publicKey)
        //        && reader.TryRead(out byte registered)
        //        && reader.TryRead(out Fixed8 votes))
        //    {
        //        value = new Validator(publicKey, registered != 0, votes);
        //        return true;
        //    }
        //    value = default;
        //    return false;
        //}


    }
}
