using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NeoFx.P2P
{
    public sealed class PipelineSocket : IDuplexPipe, IDisposable
    {
        private readonly Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        private readonly Pipe sendPipe = new Pipe();
        private readonly Pipe recvPipe = new Pipe();
        private readonly ILogger<PipelineSocket> log;

        public PipelineSocket(ILogger<PipelineSocket>? log = null)
        {
            this.log = log ?? NullLogger<PipelineSocket>.Instance;
        }

        public PipeReader Input => recvPipe.Reader;

        public PipeWriter Output => sendPipe.Writer;

        public EndPoint RemoteEndPoint => socket.RemoteEndPoint;

        private void Execute(CancellationToken token)
        {
            SocketReceiveAsync(token)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        log.LogError(t.Exception, nameof(SocketReceiveAsync) + " exception");
                    }
                    else
                    {
                        log.LogInformation(nameof(SocketReceiveAsync) + " completed {IsCanceled}", t.IsCanceled);
                    }
                    recvPipe.Writer.Complete(t.Exception);
                });

            SocketSendAsync(token)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        log.LogError(t.Exception, nameof(SocketSendAsync) + " exception");
                    }
                    else
                    {
                        log.LogInformation(nameof(SocketSendAsync) + " completed {IsCanceled}", t.IsCanceled);
                    }
                    sendPipe.Reader.Complete(t.Exception);
                });
        }

        public async Task ConnectAsync(string host, int port, CancellationToken token = default)
        {
            log.LogInformation("connecting to {host} : {port}", host, port);

            await socket.ConnectAsync(host, port).ConfigureAwait(false);
            Execute(token);
        }

        public async Task ConnectAsync(IPEndPoint endpoint, CancellationToken token = default)
        {
            log.LogInformation("connecting to {host} : {port}", endpoint.Address, endpoint.Port);

            await socket.ConnectAsync(endpoint).ConfigureAwait(false);
            Execute(token);
        }

        public void Dispose()
        {
            socket.Dispose();
        }

        private async Task SocketReceiveAsync(CancellationToken token)
        {
            while (true)
            {
                var memory = recvPipe.Writer.GetMemory();
                var bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None, token).ConfigureAwait(false);
                log.LogDebug("received {bytesRead} bytes from socket", bytesRead);
                if (bytesRead == 0)
                {
                    break;
                }

                recvPipe.Writer.Advance(bytesRead);
                var flushResult = await recvPipe.Writer.FlushAsync(token).ConfigureAwait(false);
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

        private async Task SocketSendAsync(CancellationToken token)
        {
            while (true)
            {
                var readResult = await sendPipe.Reader.ReadAsync(token).ConfigureAwait(false);
                log.LogDebug("sendPipe read {length} bytes {IsCanceled} {IsCompleted}", readResult.Buffer.Length, readResult.IsCanceled, readResult.IsCompleted);

                if (readResult.IsCanceled)
                {
                    throw new OperationCanceledException();
                }

                var buffer = readResult.Buffer;
                if (buffer.IsEmpty && readResult.IsCompleted)
                {
                    break;
                }

                foreach (var segment in buffer)
                {
                    await socket.SendAsync(segment, SocketFlags.None, token).ConfigureAwait(false);
                    log.LogDebug("sent {length} via socket", segment.Length);
                }

                sendPipe.Reader.AdvanceTo(buffer.End);
                log.LogDebug("sendPipe advanced to {end}", buffer.End.GetInteger());
            }
        }
    }
}
