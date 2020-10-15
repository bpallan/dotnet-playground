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
    // read up to 100k legacy customers from an import file/database/whatever
    // transform into new customers
    // send to api in batches of up to 100
    [TestClass]
    public class EndToEndExamples
    {
        private readonly int _customerCount = 100000;
        private static int _currentTestCount;
        private static Stopwatch _timer = new Stopwatch();
        private readonly CustomerService _customerService = new CustomerService();

        /// <summary>
        /// transform & save customer 1 record at a time single threaded
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task Save_OneAtATime()
        {
            _currentTestCount = _customerCount / 1000; // too slow to do full count
            var bulkDataService = new BulkCustomerDataService(_currentTestCount);
            var saveCount = 0;

            // this will load the entire list into memory
            var importCustomers = bulkDataService.GetCustomersFromImportAsync();

            await foreach (var importCustomer in importCustomers)
            {
                var customer = importCustomer.ToCustomer();
                _customerService.SaveCustomer(customer).Wait();
                saveCount++;
            }

            Assert.AreEqual(_currentTestCount, saveCount);
        }

        /// <summary>
        /// save customers 1 record at a time in parallel
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task Save_OneAtATime_Parallel()
        {
            _currentTestCount = _customerCount / 100; // too slow to do full count
            var bulkDataService = new BulkCustomerDataService(_currentTestCount);
            var saveCount = 0;

            // this will load the entire list into memory
            var importCustomers = await bulkDataService.GetCustomersFromImportAsync().ToListAsync();

            Parallel.ForEach(importCustomers,
                new ParallelOptions()
                {
                    MaxDegreeOfParallelism = 10
                }, importCustomer =>
                {
                    var customer = importCustomer.ToCustomer();

                    // parallel.foreach does not support async
                    _customerService.SaveCustomer(customer).Wait();
                    Interlocked.Increment(ref saveCount);
                });


            Assert.AreEqual(_currentTestCount, saveCount);
        }

        /// <summary>
        /// save customers in parallel using batches of 100 (hand rolled batching)
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task Save_Batch_Parallel()
        {
            _currentTestCount = _customerCount;
            var batch = new ConcurrentQueue<Customer>();
            var semaphore = new SemaphoreSlim(1, 1);
            var bulkDataService = new BulkCustomerDataService(_currentTestCount);
            var saveCount = 0;

            // this will load the entire list into memory
            var importCustomers = await bulkDataService.GetCustomersFromImportAsync().ToListAsync();

            Parallel.ForEach(importCustomers,
                new ParallelOptions()
                {
                    MaxDegreeOfParallelism = 10
                }, importCustomer =>
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

                        // save must happen outside semaphore so all threads aren't blocked waiting on api to return
                        if (customersToSave.Count > 0)
                        {
                            // parallel.foreach does not support async
                            var saveResult = _customerService.SaveCustomers(customersToSave).Result;
                            Interlocked.Add(ref saveCount, saveResult.Count);
                        }
                    }
                });

            // save any remaining customers left in the queue
            if (batch.Count > 0)
            {
                var saveResult = _customerService.SaveCustomers(batch.ToList()).Result;
                saveCount += saveResult.Count;
            }


            Assert.AreEqual(_currentTestCount, saveCount);
        }

        /// <summary>
        /// save customers in batches of 100 using TPL Data Flow
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task Save_Batch_UsingTpl()
        {
            _currentTestCount = _customerCount;
            int savedCount = 0;
            var bulkDataService = new BulkCustomerDataService(_currentTestCount);

            // setup TPL
            // note: this is VERY fast.  If there is a danger of overwhelming api or other resources consider adding BUfferBlock
            // batches of 100
            var batchBlock = new BatchBlock<ImportCustomer>(100);
            var executionOptions = new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = 10
            };

            // transform from import customer to customer
            var transformBlock = new TransformBlock<ImportCustomer[], Customer[]>(importCustomers 
                => importCustomers.Select(x => x.ToCustomer()).ToArray(),
                executionOptions);

            // send to api
            var actionBlock = new ActionBlock<Customer[]>(async customers =>
            {
                var savedCustomers = await _customerService.SaveCustomers(customers.ToList());
                Interlocked.Add(ref savedCount, savedCustomers.Count);
            }, executionOptions);

            // link blocks together to form pipeline
            var linkOptions = new DataflowLinkOptions()
            {
                PropagateCompletion = true
            };

            batchBlock.LinkTo(transformBlock, linkOptions);
            transformBlock.LinkTo(actionBlock, linkOptions);

            // unlike parallel.foreach TPL supports async
            await foreach (var importCustomer in bulkDataService.GetCustomersFromImportAsync())
            {
                await batchBlock.SendAsync(importCustomer);
            }

            // ensure work completes before exiting test
            batchBlock.Complete();
            await actionBlock.Completion;

            Assert.AreEqual(_currentTestCount, savedCount);
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
            Console.Out.WriteLine($"test took {_timer.ElapsedMilliseconds} ms to save {_currentTestCount} customers.");
            _timer.Reset();
        }
    }
}
