using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO.Pipelines;
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
        private readonly ILogger log;

        public PipelineSocket(ILoggerFactory? loggerFactory = null)
        {
            log = loggerFactory?.CreateLogger(nameof(PipelineSocket)) ?? NullLogger.Instance;
        }

        public PipeReader Input => recvPipe.Reader;

        public PipeWriter Output => sendPipe.Writer;

        public async Task ConnectAsync(string host, int port, CancellationToken token = default)
        {
            log.LogTrace("connecting to {host} : {port}", host, port);

            await socket.ConnectAsync(host, port).ConfigureAwait(false);
            PipeScheduler.ThreadPool.Schedule((_) => SocketReceiveAsync(token), null);
            PipeScheduler.ThreadPool.Schedule((_) => SocketSendAsync(token), null);
        }

        public void Dispose()
        {
            socket.Dispose();
        }

        private async void SocketReceiveAsync(CancellationToken token)
        {
            try
            {
                while (true)
                {
                    var memory = recvPipe.Writer.GetMemory();
                    var bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None, token).ConfigureAwait(false);
                    log.LogDebug("received {bytesRead} bytes from socket", bytesRead);
                    if (bytesRead == 0 || token.IsCancellationRequested)
                    {
                        break;
                    }

                    recvPipe.Writer.Advance(bytesRead);
                    var flushResult = await recvPipe.Writer.FlushAsync(token)
                        .ConfigureAwait(false);
                    log.LogDebug("Advanced and flushed {bytesRead} to receive pipe {IsCompleted} {IsCanceled}", bytesRead, flushResult.IsCompleted, flushResult.IsCanceled);
                    if (flushResult.IsCompleted
                        || flushResult.IsCanceled
                        || token.IsCancellationRequested)
                    {
                        break;
                    }
                }

                recvPipe.Writer.Complete();
                log.LogTrace("sendPipe completed {IsCancellationRequested}", token.IsCancellationRequested);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                log.LogError(ex, "{method} exception", nameof(SocketReceiveAsync));
                recvPipe.Writer.Complete(ex);
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        private async void SocketSendAsync(CancellationToken token)
        {
            try
            {
                while (true)
                {
                    var readResult = await sendPipe.Reader.ReadAsync(token).ConfigureAwait(false);
                    log.LogDebug("sendPipe read {length} bytes {IsCanceled} {IsCompleted}", readResult.Buffer.Length, readResult.IsCanceled, readResult.IsCompleted);

                    if (readResult.IsCanceled || token.IsCancellationRequested)
                    {
                        break;
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
                        if (token.IsCancellationRequested)
                        {
                            break;
                        }
                    }
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    sendPipe.Reader.AdvanceTo(buffer.End);
                    log.LogDebug("sendPipe advanced to {end}", buffer.End);
                }

                sendPipe.Reader.Complete();
                log.LogTrace("sendPipe completed {IsCancellationRequested}", token.IsCancellationRequested);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                log.LogError(ex, "{method} exception", nameof(SocketSendAsync));
                sendPipe.Reader.Complete(ex);
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }
    }
}
