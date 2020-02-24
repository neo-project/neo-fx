namespace NeoFx.RPC.Models
{

    public readonly struct Version
    {
        public readonly int Port;
        public readonly uint Nonce;
        public readonly string UserAgent;

        public Version(int port, uint nonce, string userAgent)
        {
            Port = port;
            Nonce = nonce;
            UserAgent = userAgent;
        }
    }
}
