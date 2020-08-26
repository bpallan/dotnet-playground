using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Tpl.Examples.Tests.TimedBatchBlock
{
    /// <summary>
    /// Used to execute once batch size is reached OR if a certain amount of time has passed w/out any new records
    /// Typically would be setup as a singleton (else you might as well complete regular batch block when done which will flush remaining records)
    /// Thread lock stuff was added specifically to allow rebus to be aware of and handle failures
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class TimedBatchBlock<T>
    {
        private readonly BatchBlock<ThreadLockWrapper<T>> _batchBlock;
        private readonly Timer _timer;

        private Action<ThreadLockWrapper<T>[]> _handler;

        private readonly int _threadTimeout;
        private readonly int _batchTimeout;

        public TimedBatchBlock(ITimedBatchBlockConfig config)
        {
            if (config.BatchTimeoutMs >= config.ThreadTimeoutMs)
                throw new ArgumentException("Thread timeout must be greater than Batch timeout.");

            _threadTimeout = config.ThreadTimeoutMs;
            _batchTimeout = config.BatchTimeoutMs;
            var batchSize = config.BatchSize;

            _batchBlock = new BatchBlock<ThreadLockWrapper<T>>(batchSize);
            var actionBlock = new ActionBlock<IEnumerable<ThreadLockWrapper<T>>>(t =>
            {
                try
                {
                    // Enumerate batch
                    var messages = t.ToArray();

                    // Run handler against batch
                    _handler.Invoke(messages);

                    // If handler succeeds, release threads, otherwise continue to hang thread (handled externally via timeout)
                    foreach (var message in messages)
                    {
                        message.ReleaseThread();
                    }
                }
                catch (Exception e)
                {
                    Console.Out.WriteLine("TimedBatchActionBlock: Error taking action on batch.");
                }
            });
            _batchBlock.LinkTo(actionBlock);

            _timer = new Timer(_ => { _batchBlock.TriggerBatch(); });
        }

        public async Task Queue(T message)
        {
            ResetTimer();

            // this thread wrap stuff was done to hold the thread open so that external processors (such as rebus) will be aware if it fails
            // thread will be released upon successful completion in above action block, else after the timeout will exception
            var wrapper = new ThreadLockWrapper<T>(message, _threadTimeout);
            await _batchBlock.SendAsync(wrapper);
            var result = await wrapper.HoldThread();
            if (!result)
            {
                throw new TimeoutException("Message timed out.");
            }
        }

        public void SetHandler(Action<ThreadLockWrapper<T>[]> handler) => _handler = handler;

        private void ResetTimer() => _timer.Change(_batchTimeout, Timeout.Infinite);
    }
}
