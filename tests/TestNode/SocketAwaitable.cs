using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace NeoFx.TestNode
{
    // https://devblogs.microsoft.com/pfxteam/awaiting-socket-operations/
    sealed class SocketAwaitable : INotifyCompletion
    {
        private readonly static Action SENTINEL = () => { };

        internal bool wasCompleted;
        internal Action? continuation;
        internal SocketAsyncEventArgs eventArgs;

        public SocketAwaitable(SocketAsyncEventArgs eventArgs)
        {
            if (eventArgs == null) throw new ArgumentNullException(nameof(eventArgs));
            this.eventArgs = eventArgs;
            eventArgs.Completed += delegate
            {
                var prev = continuation ?? Interlocked.CompareExchange(
                    ref continuation, SENTINEL, null);
                if (prev != null) prev();
            };
        }

        internal void Reset()
        {
            wasCompleted = false;
            continuation = null;
        }

        public SocketAwaitable GetAwaiter() { return this; }

        public bool IsCompleted { get { return wasCompleted; } }

        public void OnCompleted(Action _continuation)
        {
            if (continuation == SENTINEL ||
                Interlocked.CompareExchange(
                    ref continuation, _continuation, null) == SENTINEL)
            {
                Task.Run(_continuation);
            }
        }

        public void GetResult()
        {
            if (eventArgs.SocketError != SocketError.Success)
                throw new SocketException((int)eventArgs.SocketError);
        }
    }
}
