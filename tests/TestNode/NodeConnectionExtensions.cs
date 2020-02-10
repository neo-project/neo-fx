using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NeoFx.P2P;
using NeoFx.P2P.Messages;

namespace NeoFx.TestNode
{
    static class NodeConnectionExtensions
    {
        public static async IAsyncEnumerable<Message> ReceiveMessages(this INodeConnection connection, uint magic, [EnumeratorCancellation] CancellationToken token = default)
        {
            while (true)
            {
                Message message;
                try
                {
                    message = await connection.ReceiveMessage(magic, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                yield return message;
            }
        }
    }
}
