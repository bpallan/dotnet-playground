using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tpl.Examples.Tests.Extensions;
using Tpl.Examples.Tests.Models;
using Tpl.Examples.Tests.Services;

namespace Tpl.Examples.Tests
{
    // read 1 million legacy customers from an import file/database/whatever
    // transform into new customers
    // send to api in batches of 100
    [TestClass]
    public class EndToEndExamples
    {
        private readonly int _customerCount = 1000000;
        private static Stopwatch _timer = new Stopwatch();

        [TestMethod]
        public async Task Save_OneAtATime()
        {
            var reducedCount = _customerCount / 1000; // too slow to do full count
            var bulkDataService = new BulkCustomerDataService(reducedCount);
            var customerService = new CustomerService();
            var saveCount = 0;

            // this will load the entire list into memory
            var importCustomers = bulkDataService.GetCustomersFromImport();

            await foreach (var importCustomer in importCustomers)
            {
                var customer = importCustomer.ToCustomer();
                customerService.SaveCustomer(customer).Wait();
                saveCount++;
            }

            Assert.AreEqual(reducedCount, saveCount);
        }

        [TestMethod]
        public async Task Save_OneAtATime_Parallel()
        {
            var reducedCount = _customerCount / 1000; // too slow to do full count
            var bulkDataService = new BulkCustomerDataService(reducedCount);
            var customerService = new CustomerService();
            var saveCount = 0;

            // this will load the entire list into memory
            var importCustomers = await bulkDataService.GetCustomersFromImport().ToListAsync();

            Parallel.ForEach(importCustomers, (importCustomer) =>
            {
                var customer = importCustomer.ToCustomer();
                customerService.SaveCustomer(customer).Wait();
                Interlocked.Increment(ref saveCount);
            });


            Assert.AreEqual(reducedCount, saveCount);
        }

        [TestMethod]
        public async Task Save_Batch_Parallel()
        {
            
        }

        [TestMethod]
        public async Task Save_Batch_UsingTpl()
        {

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
