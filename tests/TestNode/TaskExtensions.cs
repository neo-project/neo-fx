using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
using System;
using System.Threading.Tasks;

namespace NeoFx.TestNode
{
    static class TaskExtensions
    {
        public static void LogResult(this Task task, ILogger log, string name, Action<Exception?>? onComplete = null)
        {
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    var ex = t.Exception?.InnerExceptions?.Count == 1
                            ? t.Exception?.InnerExceptions?[0]
                            : t.Exception;
                    log.LogError(ex, name + " exception");
                }
                else
                {
                    log.LogInformation(name + " {status}", t.Status);
                }
                onComplete?.Invoke(t.Exception);
            }).Forget();
        }

        // https://github.com/dotnet/runtime/issues/31503#issuecomment-554415966
        public static ValueTask AsValueTask<T>(this ValueTask<T> valueTask)
        {
            if (valueTask.IsCompletedSuccessfully)
            {
                var _ = valueTask.GetAwaiter().GetResult();
                return default;
            }

            return new ValueTask(valueTask.AsTask());
        }
    }
}
