using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Tpl.Examples.Tests.Extensions;
using Tpl.Examples.Tests.Models;
using Tpl.Examples.Tests.Services;

namespace Tpl.Examples.Tests
{
    // https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/dataflow-task-parallel-library
    [TestClass]
    public class DataFlow_BasicExamples
    {
        private static readonly DataflowBlockOptions DataOptions = new DataflowBlockOptions()
        {
            BoundedCapacity = 10 // reject messages if 10 already in queue waiting
        };

        private static readonly ExecutionDataflowBlockOptions ExecutionOptions = new ExecutionDataflowBlockOptions()
        {
            MaxDegreeOfParallelism = 10
        };

        // allows complete to propagate across all blocks instead of having to call/await it on every block in the chain
        private static readonly DataflowLinkOptions LinkOptions = new DataflowLinkOptions()
        {
            PropagateCompletion = true 
        };

        /// <summary>
        /// Buffer block is used to control the flow of data into your pipeline
        /// When the buffer is full, new records will be rejected when you call the Post method
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task BufferBlock_PostRejectsMessageOnceFull()
        {
            var bufferBlock = new BufferBlock<ImportCustomer>(DataOptions); 
            var actionBlock = new ActionBlock<ImportCustomer>(importCustomer => PerformAction(importCustomer), ExecutionOptions);
            bufferBlock.LinkTo(actionBlock, LinkOptions);

            var bulkService = new BulkCustomerDataService(100);

            await foreach (var customer in bulkService.GetCustomersFromImportAsync())
            {
                // this will fail once the block reaches capacity
                bufferBlock.Post(customer);
            }

            // ensure work completes prior to test exiting
            bufferBlock.Complete();
            await actionBlock.Completion;

            // not all actions complete because post rejected records
            Assert.IsTrue(_performActionCount < 100, $"Action count is {_performActionCount}");
        }


        /// <summary>
        /// You can use a loop to wait for the buffer to have capacity before posting more records to it
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task BufferBlock_PostWaitForBufferSpace()
        {
            var bufferBlock = new BufferBlock<ImportCustomer>(DataOptions);
            var actionBlock = new ActionBlock<ImportCustomer>(importCustomer => PerformAction(importCustomer),
                ExecutionOptions);
            bufferBlock.LinkTo(actionBlock, LinkOptions);

            var bulkService = new BulkCustomerDataService(100);

            await foreach (var customer in bulkService.GetCustomersFromImportAsync())
            {
                // wait until block has capacity before sending more records
                while (bufferBlock.Post(customer) == false)
                {
                    await Task.Delay(10);
                }
            }

            // ensure work completes prior to test exiting
            bufferBlock.Complete();
            await actionBlock.Completion;

            // not all actions complete because post rejected records
            Assert.AreEqual(100, _performActionCount, $"Action count is {_performActionCount}");
        }

        /// <summary>
        /// You can also use SendAsync which will return an incomplete Task that can be waited for when the buffer is full
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task BufferBlock_SendAsyncWillWait()
        {
            var bufferBlock = new BufferBlock<ImportCustomer>(DataOptions);
            var actionBlock = new ActionBlock<ImportCustomer>(importCustomer => PerformAction(importCustomer),
                ExecutionOptions);
            bufferBlock.LinkTo(actionBlock, LinkOptions);

            var bulkService = new BulkCustomerDataService(100);

            await foreach (var customer in bulkService.GetCustomersFromImportAsync())
            {
                // send async will return an incomplete task when block is full that we can wait on
                await bufferBlock.SendAsync(customer);
            }

            // ensure work completes prior to test exiting
            bufferBlock.Complete();
            await actionBlock.Completion;

            // not all actions complete because post rejected records
            Assert.AreEqual(100, _performActionCount, $"Action count is {_performActionCount}");
        }

        /// <summary>
        /// BroadcastBlock is used when you want to fork your pipeline
        /// Each block after broadcast will receive the message posted to the block
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task BroadcastBlock_Example()
        {
            var broadcastBlock = new BroadcastBlock<ImportCustomer>(null, new DataflowBlockOptions());
            var actionBlock1 = new ActionBlock<ImportCustomer>(importCustomer => PerformAction(importCustomer, "Action-1", 50), ExecutionOptions);
            var actionBlock2 = new ActionBlock<ImportCustomer>(importCustomer => PerformAction(importCustomer, "Action-2", 100), ExecutionOptions);
            broadcastBlock.LinkTo(actionBlock1, LinkOptions);
            broadcastBlock.LinkTo(actionBlock2, LinkOptions);

            var bulkService = new BulkCustomerDataService(100);

            await foreach (var customer in bulkService.GetCustomersFromImportAsync())
            {
                await broadcastBlock.SendAsync(customer);
            }

            // ensure work completes prior to test exiting
            broadcastBlock.Complete();
            Task.WaitAll(actionBlock1.Completion, actionBlock2.Completion);

            // each action block received every message
            Assert.AreEqual(200, _performActionCount, $"Action count is {_performActionCount}");
        }

        /// <summary>
        /// ActionBlock is typically at the end of your pipeline
        /// It accepts input and performs an action
        /// </summary>
        /// <returns></returns>

        [TestMethod]
        public async Task ActionBlock_Example()
        {
            var actionBlock = new ActionBlock<ImportCustomer>(importCustomer => PerformAction(importCustomer), ExecutionOptions);

            var bulkService = new BulkCustomerDataService(10);

            await foreach (var customer in bulkService.GetCustomersFromImportAsync())
            {
                await actionBlock.SendAsync(customer);
            }

            // ensure work completes prior to test exiting
            actionBlock.Complete();
            await actionBlock.Completion;
        }

        /// <summary>
        /// TransformBlock receives data, performs actions on it, and then sends it on
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TransformBlock_Example()
        {
            var transformBlock = new TransformBlock<ImportCustomer, Customer>(importCustomer => importCustomer.ToCustomer(), ExecutionOptions);
            var actionBlock = new ActionBlock<Customer>(customer => PerformAction(customer), ExecutionOptions);
            transformBlock.LinkTo(actionBlock, LinkOptions);

            var bulkService = new BulkCustomerDataService(100);

            await foreach (var importCustomer in bulkService.GetCustomersFromImportAsync())
            {
                await transformBlock.SendAsync(importCustomer);
            }

            // ensure work completes prior to test exiting
            transformBlock.Complete();
            await actionBlock.Completion;

            Assert.AreEqual(100, _performActionCount, $"Action count is {_performActionCount}");
        }

        /// <summary>
        /// TransformManyBlock works similar to a SelectMany statement
        /// It receives a single input and returns an IEnumerable of whatever output time it creates
        /// In this example we are receiving json input and return a list of objects
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TransformManyBlock_Example()
        {
            var transformManyBlock = new TransformManyBlock<string, ImportCustomer>(JsonConvert.DeserializeObject<List<ImportCustomer>>, ExecutionOptions);
            var actionBlock = new ActionBlock<ImportCustomer>(importCustomer => PerformAction(importCustomer), ExecutionOptions);
            transformManyBlock.LinkTo(actionBlock, LinkOptions);

            var bulkService = new BulkCustomerDataService(100);
            var json = JsonConvert.SerializeObject(await bulkService.GetCustomersFromImportAsync().ToListAsync());

            transformManyBlock.Post(json);

            // ensure work completes prior to test exiting
            transformManyBlock.Complete();
            await actionBlock.Completion;

            Assert.AreEqual(100, _performActionCount, $"Action count is {_performActionCount}");
        }

        /// <summary>
        /// BatchBlock is useful when you want to group items into a particular batch size to perform an action on the batch
        /// Batching records to send to an api or database is a good use case for this
        /// Warning:  If you don't complete the block, then items can get stuck in it for a long time in low volume situations
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task BatchBlock_Example()
        {
            var batchBlock = new BatchBlock<ImportCustomer>(10);
            var actionBlock = new ActionBlock<ImportCustomer[]>(importCustomer => PerformAction(importCustomer), ExecutionOptions);
            batchBlock.LinkTo(actionBlock, LinkOptions);

            var bulkService = new BulkCustomerDataService(100);

            await foreach (var importCustomer in bulkService.GetCustomersFromImportAsync())
            {
                await batchBlock.SendAsync(importCustomer);
            }

            // ensure work completes before exiting test
            batchBlock.Complete();
            await actionBlock.Completion;

            Assert.AreEqual(10, _performActionCount, $"Action count is {_performActionCount}");
        }

        private static int _performActionCount = 0;

        private async Task PerformAction<T>(T input, string actionId = null, int delayMs = 100)
        {
            Interlocked.Increment(ref _performActionCount);

            await Task.Delay(delayMs);
            Console.WriteLine($@"{(actionId == null ? "" : $"Action {actionId}: ")}{JsonConvert.SerializeObject(input)}");
        }

        [TestInitialize]
        public void TestInit()
        {
            _performActionCount = 0;
        }
    }
}
