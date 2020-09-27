using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Tpl.Examples.Tests.Extensions;
using Tpl.Examples.Tests.Models;
using Tpl.Examples.Tests.Services;
using Tpl.Examples.Tests.TimedBatchBlock;

namespace Tpl.Examples.Tests
{
    [TestClass]
    public class TimedBatchBlockExamples
    {
        private static readonly ExecutionDataflowBlockOptions ExecutionOptions = new ExecutionDataflowBlockOptions()
        {
            MaxDegreeOfParallelism = 10
        };

        private static readonly DataflowLinkOptions LinkOptions = new DataflowLinkOptions()
        {
            PropagateCompletion = true
        };

        /// <summary>
        /// Send 95 records in batches of 10.  The last 5 will get held until the timeout has passed.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TimedBatchBlock_Example()
        {
            // setup timed batch block
            var timedBatchBlock = new TimedBatchBlock<Customer>(new TimedBackBlockConfig());
            timedBatchBlock.SetHandler(async customers => await PerformAction(customers, 1, 1));

            List<Task> tasks = new List<Task>();
            var bulkService = new BulkCustomerDataService(95);

            await foreach (var importCustomer in bulkService.GetCustomersFromImport())
            {
                // this is designed to be executed in many separate threads (using rebus typically)
                // if you await here it will wait the full 10 seconds for each message
                tasks.Add(timedBatchBlock.Queue(importCustomer.ToCustomer()));
            }

            // timed block will still have the last 5 records queued up waiting for more
            await Task.Delay(1000);
            Assert.AreEqual(9, _performActionCount, "Batch block should not be finished yet.");

            await Task.Delay(2000);
            Assert.AreEqual(10, _performActionCount, "Batch block should be done.");

            Task.WaitAll(tasks.ToArray());
        }

        private static int _performActionCount = 0;

        private async Task PerformAction<T>(T input, int actionId, int delayMs)
        {
            Interlocked.Increment(ref _performActionCount);

            await Task.Delay(delayMs);
            Console.WriteLine($"Action {actionId}: {JsonConvert.SerializeObject(input)}");
        }

        private class TimedBackBlockConfig : ITimedBatchBlockConfig
        {
            public int BatchSize { get; } = 10;
            public int BatchTimeoutMs { get; } = 2000;

            /// <summary>
            /// If batch has not finished successfully after this time, then release tasks and return exception
            /// </summary>
            public int ThreadTimeoutMs => (BatchSize * BatchTimeoutMs) + BatchTimeoutMs;
        }
    }
}
