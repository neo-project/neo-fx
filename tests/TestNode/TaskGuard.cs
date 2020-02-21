using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NeoFx.TestNode
{
    class TaskGuard
    {
        DateTimeOffset lastCheck = DateTimeOffset.MinValue;
        int running = 0;
        readonly Func<CancellationToken, Task> action;
        readonly string name;
        readonly ILogger logger;
        readonly TimeSpan guardTime;
        readonly bool hasZeroGuardTime;

        public TaskGuard(Func<CancellationToken, Task> action, string name, ILogger logger, double guardTime)
            : this(action, name, logger, TimeSpan.FromSeconds(guardTime))
        {
        }

        public TaskGuard(Func<CancellationToken, Task> action, string name, ILogger logger, TimeSpan guardTime = default)
        {
            this.action = action;
            this.name = name;
            this.logger = logger;
            this.guardTime = guardTime;
            this.hasZeroGuardTime = guardTime.Ticks == 0;
        }

        public void Run(CancellationToken token)
        {
            if (running == 0 && 
                (hasZeroGuardTime || lastCheck.Add(guardTime) <= DateTimeOffset.Now))
            {
                RunAsync(token).LogResult(logger, name);
            }
        }

        async Task RunAsync(CancellationToken token)
        {
            if (Interlocked.CompareExchange(ref running, 1, 0) != 0)
                return;

            try
            {
                await action(token);
            }
            finally
            {
                lastCheck = DateTimeOffset.Now;
                running = 0;
            }
        }
    }
}
