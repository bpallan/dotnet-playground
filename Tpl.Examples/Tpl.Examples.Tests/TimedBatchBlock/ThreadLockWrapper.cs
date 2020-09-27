using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tpl.Examples.Tests.TimedBatchBlock
{
    /// <summary>
    /// Holds a task open until explicitly release or after a timespan has elapsed
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    internal class ThreadLockWrapper<TMessage>
    {
        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(0, 1);
        private readonly int _timeoutMilliseconds;

        public ThreadLockWrapper(TMessage message, int timeoutMilliseconds)
        {
            Message = message;
            _timeoutMilliseconds = timeoutMilliseconds;
        }

        /// <summary>
        /// Data being wrapped
        /// </summary>
        public TMessage Message { get; }

        /// <summary>
        /// Keep task open until timespan or ReleaseThread is called
        /// </summary>
        /// <returns></returns>
        public Task<bool> HoldThread() => _semaphoreSlim.WaitAsync(TimeSpan.FromMilliseconds(_timeoutMilliseconds));

        /// <summary>
        /// Release the task
        /// </summary>
        public void ReleaseThread() => _semaphoreSlim.Release();
    }
}
