using System.Net.Sockets;

namespace NeoFx.TestNode
{
    // https://devblogs.microsoft.com/pfxteam/awaiting-socket-operations/
    static class SocketExtensions
    {
        public static SocketAwaitable ReceiveAsync(this Socket socket,
            SocketAwaitable awaitable)
        {
            awaitable.Reset();
            if (!socket.ReceiveAsync(awaitable.eventArgs))
                awaitable.wasCompleted = true;
            return awaitable;
        }

        public static SocketAwaitable SendAsync(this Socket socket,
            SocketAwaitable awaitable)
        {
            awaitable.Reset();
            if (!socket.SendAsync(awaitable.eventArgs)) 
                awaitable.wasCompleted = true;
            return awaitable;
        }

        public static SocketAwaitable ConnectAsync(this Socket socket, 
            SocketAwaitable awaitable)
        {
            awaitable.Reset();
            if (!socket.ConnectAsync(awaitable.eventArgs))
                awaitable.wasCompleted = true;
            return awaitable;
        }
    }
}
