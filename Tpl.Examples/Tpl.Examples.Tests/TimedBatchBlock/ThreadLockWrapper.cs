using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tpl.Examples.Tests.TimedBatchBlock
{
    internal class ThreadLockWrapper<TMessage>
    {
        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(0, 1);
        private readonly int _timeoutMilliseconds;

        public ThreadLockWrapper(TMessage message, int timeoutMilliseconds)
        {
            Message = message;
            _timeoutMilliseconds = timeoutMilliseconds;
        }

        public TMessage Message { get; }

        public Task<bool> HoldThread() => _semaphoreSlim.WaitAsync(TimeSpan.FromMilliseconds(_timeoutMilliseconds));

        public void ReleaseThread() => _semaphoreSlim.Release();
    }
}
