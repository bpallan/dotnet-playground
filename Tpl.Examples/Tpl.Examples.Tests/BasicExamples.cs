using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tpl.Examples.Tests.Models;
using Tpl.Examples.Tests.Services;

namespace Tpl.Examples.Tests
{
    // https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/dataflow-task-parallel-library
    [TestClass]
    public class BasicExamples
    {
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
        }

        [TestMethod]
        public async Task BroadcastBlock_Example1()
        {
            // todo: try async
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

        private void PerformAction2(ImportCustomer arg)
        {
            Task.Delay(50).Wait();
            Console.Out.WriteLine($"Action 2: {arg.Id}");
        }

        private void PerformAction1(ImportCustomer arg)
        {
            Task.Delay(100).Wait();
            Console.Out.WriteLine($"Action 1: {arg.Id}");
        }
    }
}
