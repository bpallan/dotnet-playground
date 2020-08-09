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
    public class BasicExamples
    {
        private static readonly ExecutionDataflowBlockOptions ExecutionOptions = new ExecutionDataflowBlockOptions()
        {
            MaxDegreeOfParallelism = 10
        };

        // allows complete to propagate across all blocks instead of having to call/await it on every block in the chain
        private static readonly DataflowLinkOptions LinkOptions = new DataflowLinkOptions()
        {
            PropagateCompletion = true 
        };

        /////////////////////////
        /// BUFFERING BLOCKS ///
        ///////////////////////
        [TestMethod]
        public async Task BufferBlock_Example1()
        {
            _performActionCount = 0;
            var bufferBlock = new BufferBlock<ImportCustomer>(new DataflowBlockOptions()); //todo: explore bounded capacity, setting it to lower than size of input causes input to not be processed
            var actionBlock = new ActionBlock<ImportCustomer>(importCustomer => PerformAction(importCustomer, 1, 1), ExecutionOptions);
            bufferBlock.LinkTo(actionBlock, LinkOptions);

            var bulkService = new BulkCustomerDataService(100);

            await foreach (var customer in bulkService.GetCustomersFromImport())
            {
                bufferBlock.Post(customer);
            }

            bufferBlock.Complete();
            await actionBlock.Completion;

            Assert.AreEqual(100, _performActionCount, $"Action count is {_performActionCount}");
        }

        [TestMethod]
        public async Task BroadcastBlock_Example1()
        {
            _performActionCount = 0;
            var broadcastBlock = new BroadcastBlock<ImportCustomer>(null, new DataflowBlockOptions());
            var actionBlock1 = new ActionBlock<ImportCustomer>(importCustomer => PerformAction(importCustomer, 1, 50));
            var actionBlock2 = new ActionBlock<ImportCustomer>(importCustomer => PerformAction(importCustomer, 2, 100));
            broadcastBlock.LinkTo(actionBlock1, LinkOptions);
            broadcastBlock.LinkTo(actionBlock2, LinkOptions);

            var bulkService = new BulkCustomerDataService(100);

            await foreach (var customer in bulkService.GetCustomersFromImport())
            {
                broadcastBlock.Post(customer);
            }

            broadcastBlock.Complete();
            Task.WaitAll(actionBlock1.Completion, actionBlock2.Completion);

            Assert.AreEqual(200, _performActionCount, $"Action count is {_performActionCount}");
        }

        private static SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private static int _performActionCount = 0;

        private async Task PerformAction<T>(T input, int actionId, int delayMs)
        {
            await _semaphore.WaitAsync();
            _performActionCount++;
            _semaphore.Release();

            await Task.Delay(delayMs);
            Console.WriteLine($"Action {actionId}: {JsonConvert.SerializeObject(input)}");
        }

        /////////////////////////
        /// EXECUTION BLOCKS ///
        ///////////////////////

        [TestMethod]
        public async Task ActionBlock_Example1()
        {
            var actionBlock = new ActionBlock<ImportCustomer>(importCustomer => PerformAction(importCustomer, 1, 1), ExecutionOptions);

            var bulkService = new BulkCustomerDataService(10);

            await foreach (var customer in bulkService.GetCustomersFromImport())
            {
                actionBlock.Post(customer);
            }

            // must call or will never complete
            actionBlock.Complete();

            // must call or test will end before finished
            await actionBlock.Completion;
        }

        [TestMethod]
        public async Task TransformBlock_Example1()
        {
            var transformBlock = new TransformBlock<ImportCustomer, Customer>(importCustomer => importCustomer.ToCustomer(), ExecutionOptions);

            var bulkService = new BulkCustomerDataService(10);

            await foreach (var importCustomer in bulkService.GetCustomersFromImport())
            {
                transformBlock.Post(importCustomer);
            }

            // todo: why does this not work unless you step through
            //while (transformBlock.TryReceive(out Customer customer))
            //{
            //    Console.Out.WriteLine($"Received: {customer.Id} - {customer.ExternalId}");
            //}

            // if you don't receive every record then the block never completes
            for (int i = 0; i < 10; i++)
            {
                var customer = await transformBlock.ReceiveAsync();
                Console.Out.WriteLine($"Received: {customer.Id} - {customer.ExternalId}");
            }

            transformBlock.Complete();

            await transformBlock.Completion;
        }

        [TestMethod]
        public async Task TransformManyBlock_Example1()
        {
            var transformManyBlock = new TransformManyBlock<string, ImportCustomer>(JsonConvert.DeserializeObject<List<ImportCustomer>>, ExecutionOptions);

            var bulkService = new BulkCustomerDataService(10);
            var json = JsonConvert.SerializeObject(await bulkService.GetCustomersFromImport().ToListAsync());

            transformManyBlock.Post(json);

            // must receive all messages or code will never exit
            for (int i = 0; i < 10; i++)
            {
                var customer = await transformManyBlock.ReceiveAsync();
                Console.Out.WriteLine($"Received: {customer.Id}");
            }

            transformManyBlock.Complete();

            await transformManyBlock.Completion;
        }
    }
}
