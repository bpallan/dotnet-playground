using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tpl.Examples.Tests.Extensions;
using Tpl.Examples.Tests.Models;
using Tpl.Examples.Tests.Services;

namespace Tpl.Examples.Tests
{
    // read many legacy customers from an import file/database/whatever
    // transform into new customers
    // send to api in batches of 100
    [TestClass]
    public class EndToEndExamples
    {
        private readonly int _customerCount = 100000;
        private static Stopwatch _timer = new Stopwatch();
        private readonly CustomerService _customerService = new CustomerService();

        [TestMethod]
        public async Task Save_OneAtATime()
        {
            var reducedCount = _customerCount / 100; // too slow to do full count
            var bulkDataService = new BulkCustomerDataService(reducedCount);
            var saveCount = 0;

            // this will load the entire list into memory
            var importCustomers = bulkDataService.GetCustomersFromImport();

            await foreach (var importCustomer in importCustomers)
            {
                var customer = importCustomer.ToCustomer();
                _customerService.SaveCustomer(customer).Wait();
                saveCount++;
            }

            Assert.AreEqual(reducedCount, saveCount);
        }

        [TestMethod]
        public async Task Save_OneAtATime_Parallel()
        {
            var reducedCount = _customerCount / 100; // too slow to do full count
            var bulkDataService = new BulkCustomerDataService(reducedCount);
            var saveCount = 0;

            // this will load the entire list into memory
            var importCustomers = await bulkDataService.GetCustomersFromImport().ToListAsync();

            Parallel.ForEach(importCustomers, (importCustomer) =>
            {
                var customer = importCustomer.ToCustomer();
                _customerService.SaveCustomer(customer).Wait();
                Interlocked.Increment(ref saveCount);
            });


            Assert.AreEqual(reducedCount, saveCount);
        }

        [TestMethod]
        public async Task Save_Batch_Parallel()
        {
            var batch = new ConcurrentQueue<Customer>();
            var semaphore = new SemaphoreSlim(1, 1);

            var bulkDataService = new BulkCustomerDataService(_customerCount);
            var saveCount = 0;

            // this will load the entire list into memory
            var importCustomers = await bulkDataService.GetCustomersFromImport().ToListAsync();

            Parallel.ForEach(importCustomers, (importCustomer) =>
            {
                var customer = importCustomer.ToCustomer();
                batch.Enqueue(customer);

                if (batch.Count >= 100)
                {
                    var customersToSave = new List<Customer>();

                    // ensure only 1 thread is sucking records from queue at a time
                    semaphore.Wait();

                    if (batch.Count >= 100)
                    {
                        for (int i = 0; i < 100; i++)
                        {
                            if (batch.TryDequeue(out var batchCustomer))
                            {
                                customersToSave.Add(batchCustomer);
                            }
                        }
                    }

                    semaphore.Release();
                    var saveResult = _customerService.SaveCustomers(customersToSave).Result;
                    Interlocked.Add(ref saveCount, saveResult.Count);
                }
            });

            if (batch.Count > 0)
            {
                var saveResult = _customerService.SaveCustomers(batch.ToList()).Result;
                Interlocked.Add(ref saveCount, saveResult.Count);
            }


            Assert.AreEqual(_customerCount, saveCount);
        }

        [TestMethod]
        public async Task Save_Batch_UsingTpl()
        {
            int savedCount = 0;
            var bulkDataService = new BulkCustomerDataService(_customerCount);
            var batchBlock = new BatchBlock<ImportCustomer>(100);
            var executionOptions = new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = 10
            };
            var transformBlock = new TransformBlock<ImportCustomer[], Customer[]>(importCustomers => importCustomers.Select(x => x.ToCustomer()).ToArray());
            var actionBlock = new ActionBlock<Customer[]>(async customers =>
            {
                var savedCustomers = await _customerService.SaveCustomers(customers.ToList());
                Interlocked.Add(ref savedCount, savedCustomers.Count);
            }, executionOptions);

            var linkOptions = new DataflowLinkOptions()
            {
                PropagateCompletion = true
            };

            batchBlock.LinkTo(transformBlock, linkOptions);
            transformBlock.LinkTo(actionBlock, linkOptions);

            await foreach (var importCustomer in bulkDataService.GetCustomersFromImport())
            {
                await batchBlock.SendAsync(importCustomer);
            }

            // ensure work completes before exiting test
            batchBlock.Complete();
            await actionBlock.Completion;

            Assert.AreEqual(_customerCount, savedCount);
        }

        [TestInitialize]
        public void TestInit()
        {
            _timer.Start();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _timer.Stop();
            Console.Out.WriteLine($"test time: {_timer.ElapsedMilliseconds} ms");
            _timer.Reset();
        }
    }
}
