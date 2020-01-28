using Microsoft.Extensions.Logging;

namespace NeoFx.P2P.Messages
{
    public sealed class GetAddrMessage : Message
    {
        public const string CommandText = "getaddr";

        public GetAddrMessage(in MessageHeader header) : base(header)
        {
        }

        public override void LogMessage(ILogger logger)
        {
            logger.LogInformation("Receive {messageType}",
                nameof(GetAddrMessage));
        }
    }
}
