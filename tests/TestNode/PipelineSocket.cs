using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace NeoFx.TestNode
{
    public interface IPipelineSocket : IDuplexPipe, IDisposable
    {
        EndPoint RemoteEndPoint { get; }
        Task ConnectAsync(IPEndPoint endpoint, CancellationToken token = default);
    }

    public sealed class PipelineSocket : IPipelineSocket, IDisposable
    {
        private readonly Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        private readonly Pipe sendPipe = new Pipe();
        private readonly Pipe recvPipe = new Pipe();
        private readonly ILogger<PipelineSocket> log;

        public PipeReader Input => recvPipe.Reader;
        public PipeWriter Output => sendPipe.Writer;
        public EndPoint RemoteEndPoint => socket.RemoteEndPoint;

        public PipelineSocket(ILogger<PipelineSocket>? log = null)
        {
            this.log = log ?? NullLogger<PipelineSocket>.Instance;
        }

        public void Dispose()
        {
            socket.Dispose();
        }

        public async Task ConnectAsync(IPEndPoint endpoint, CancellationToken token = default)
        {
            log.LogInformation("connecting to {host}:{port}", endpoint.Address, endpoint.Port);

            var args = new SocketAsyncEventArgs();
            args.RemoteEndPoint = endpoint;
            var awaitable = new SocketAwaitable(args);

            await socket.ConnectAsync(awaitable);
            Execute(token);
        }

        private void Execute(CancellationToken token)
        {
            StartSocketReceive(token)
                .LogResult(log, nameof(StartSocketReceive),
                    ex => recvPipe.Writer.Complete(ex));

            StartSocketSend(token)
                .LogResult(log, nameof(StartSocketSend),
                    ex => sendPipe.Reader.Complete(ex));
        }

        private async Task StartSocketReceive(CancellationToken token)
        {
            var writer = recvPipe.Writer;
            var args = new SocketAsyncEventArgs();
            var awaitable = new SocketAwaitable(args);

            while (!token.IsCancellationRequested)
            {
                var memory = writer.GetMemory();
                args.SetBuffer(memory);

                await socket.ReceiveAsync(awaitable);
                var bytesRead = args.BytesTransferred;
                log.LogDebug("received {bytesRead} bytes from socket", bytesRead);
                if (bytesRead == 0)
                {
                    break;
                }

                writer.Advance(bytesRead);
                var flushResult = await writer.FlushAsync(token).ConfigureAwait(false);
                log.LogDebug("Advanced and flushed {bytesRead} to receive pipe {IsCompleted} {IsCanceled}", bytesRead, flushResult.IsCompleted, flushResult.IsCanceled);
                if (flushResult.IsCompleted)
                {
                    break;
                }

                if (flushResult.IsCanceled)
                {
                    throw new OperationCanceledException();
                }
            }
        }

        private async Task StartSocketSend(CancellationToken token)
        {
            var reader = sendPipe.Reader;
            var args = new SocketAsyncEventArgs();
            var awaitable = new SocketAwaitable(args);

            while (!token.IsCancellationRequested)
            {
                var readResult = await reader.ReadAsync(token).ConfigureAwait(false);
                log.LogDebug("sendPipe read {length} bytes {IsCanceled} {IsCompleted}", readResult.Buffer.Length, readResult.IsCanceled, readResult.IsCompleted);

                if (readResult.IsCanceled)
                {
                    throw new OperationCanceledException();
                }

                var buffer = readResult.Buffer;
                if (buffer.Length > 0)
                {
                    foreach (var segment in buffer)
                    {
                        args.SetBuffer(MemoryMarshal.AsMemory(segment));
                        await socket.SendAsync(awaitable);
                        log.LogDebug("sent {length} via socket", segment.Length);
                    }
                }

                reader.AdvanceTo(buffer.End);
                log.LogDebug("sendPipe advanced to {end}", buffer.End.GetInteger());

                if (readResult.IsCompleted)
                {
                    break;
                }
            }
        }
    }
}
