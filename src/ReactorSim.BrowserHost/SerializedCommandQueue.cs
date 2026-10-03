using System;
using System.Threading;
using System.Threading.Tasks;

namespace ReactorSim.BrowserHost
{
    // The JS entry thread only submits work. A single ordered command stream owns
    // all bridge state; numerical row partitions may run concurrently inside it.
    internal sealed class SerializedCommandQueue
    {
        private readonly object _gate = new object();
        private Task _tail = Task.CompletedTask;

        public Task<string> Enqueue(Func<string> operation)
        {
            ArgumentNullException.ThrowIfNull(operation);
            lock (_gate)
            {
                Task predecessor = _tail;
                Task<string> result = Task.Run(async () =>
                {
                    await predecessor.ConfigureAwait(false);
                    return operation();
                });
                // Observe faults without poisoning later requests. Each caller still
                // receives its own result or exception through the exported promise.
                _tail = result.ContinueWith(completed => { _ = completed.Exception; },
                    CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                return result;
            }
        }
    }
}
