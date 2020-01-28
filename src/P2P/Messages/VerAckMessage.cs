using Microsoft.Extensions.Logging;

namespace NeoFx.P2P.Messages
{
    public sealed class VerAckMessage : Message
    {
        public const string CommandText = "verack";

        public VerAckMessage(in MessageHeader header) : base(header)
        {
        }

        public override void LogMessage(ILogger logger)
        {
            logger.LogInformation("Receive {messageType}",
                nameof(VerAckMessage));
        }
    }
}
