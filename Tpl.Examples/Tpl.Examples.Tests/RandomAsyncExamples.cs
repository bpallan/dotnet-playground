using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tpl.Examples.Tests.Extensions;
using Tpl.Examples.Tests.Models;
using Tpl.Examples.Tests.Services;

namespace Tpl.Examples.Tests
{
    [TestClass]
    public class RandomAsyncExamples
    {
        [TestMethod]
        public async Task Test3()
        {
            var bulkCustomerService = new BulkCustomerDataService(10);
            var customerService = new CustomerService();

            await foreach (var importCustomer in bulkCustomerService.GetCustomersFromImportAsync())
            {
                customerService.SaveCustomer(importCustomer.ToCustomer());
            }

            Assert.IsTrue(customerService.SavedCount < 10);
        }

        [TestMethod]
        public async Task Test1()
        {
            var bulkCustomerService = new BulkCustomerDataService(100);
            var customerService = new CustomerService();

            await foreach (var importCustomer in bulkCustomerService.GetCustomersFromImportAsync())
            {
                await customerService.SaveCustomer(importCustomer.ToCustomer());
            }

            Assert.AreEqual(customerService.SavedCount, 100);
        }

        [TestMethod]
        public async Task Test2()
        {
            var bulkCustomerService = new BulkCustomerDataService(100);
            var customerService = new CustomerService();
            List<Task> tasks = new List<Task>();

            await foreach (var importCustomer in bulkCustomerService.GetCustomersFromImportAsync())
            {
                tasks.Add(customerService.SaveCustomer(importCustomer.ToCustomer()));
            }

            Task.WaitAll(tasks.ToArray());
            Assert.AreEqual(customerService.SavedCount, 100);
        }
    }
}