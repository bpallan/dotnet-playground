using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
        /////////////////////////
        /// BUFFERING BLOCKS ///
        ///////////////////////
        [TestMethod]
        public async Task BufferBlock_Example1()
        {
            var bufferBlock = new BufferBlock<ImportCustomer>(new DataflowBlockOptions());
            var bulkService = new BulkCustomerDataService(10);

            await foreach (var customer in bulkService.GetCustomersFromImport())
            {
                bufferBlock.Post(customer);
            }

            while (bufferBlock.TryReceive(out var customerReceived))
            {
                Console.Out.WriteLine(customerReceived.Id);
            }

            // will work without this but should call when done
            bufferBlock.Complete();
        }

        [TestMethod]
        public async Task BroadcastBlock_Example1()
        {
            var broadcastBlock = new BroadcastBlock<ImportCustomer>(null);
            var actionBlock1 = new ActionBlock<ImportCustomer>(PerformAction1);
            var actionBlock2 = new ActionBlock<ImportCustomer>(PerformAction2);
            broadcastBlock.LinkTo(actionBlock1);
            broadcastBlock.LinkTo(actionBlock2);

            var bulkService = new BulkCustomerDataService(10);

            await foreach (var customer in bulkService.GetCustomersFromImport())
            {
                broadcastBlock.Post(customer);
            }

            // required or will never return
            actionBlock1.Complete();
            actionBlock2.Complete();

            // required or will exit w/out all records finishing
            Task.WaitAll(actionBlock1.Completion, actionBlock2.Completion);
        }

        private async Task PerformAction2(ImportCustomer arg)
        {
            await Task.Delay(50);
            await Console.Out.WriteLineAsync($"Action 2: {arg.Id}");
        }

        private async Task PerformAction1(ImportCustomer arg)
        {
            await Task.Delay(100);
            await Console.Out.WriteLineAsync($"Action 1: {arg.Id}");
        }

        /////////////////////////
        /// EXECUTION BLOCKS ///
        ///////////////////////

        [TestMethod]
        public async Task ActionBlock_Example1()
        {
            var actionBlock = new ActionBlock<ImportCustomer>(PerformAction1, new ExecutionDataflowBlockOptions()
            {
                // will process in order if not set
                MaxDegreeOfParallelism = 10
            });

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
            var transformBlock = new TransformBlock<ImportCustomer, Customer>(importCustomer => importCustomer.ToCustomer(), new ExecutionDataflowBlockOptions()
            {
                // will process in order if not set
                MaxDegreeOfParallelism = 10
            });

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
            var transformManyBlock = new TransformManyBlock<string, ImportCustomer>(JsonConvert.DeserializeObject<List<ImportCustomer>>, 
                new ExecutionDataflowBlockOptions()
                {
                    MaxDegreeOfParallelism = 1
                });

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
