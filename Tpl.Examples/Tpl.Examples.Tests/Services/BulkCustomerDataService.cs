using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tpl.Examples.Tests.Models;

namespace Tpl.Examples.Tests.Services
{
    public class BulkCustomerDataService
    {
        private readonly Lazy<IAsyncEnumerable<ImportCustomer>> _customerList;

        public BulkCustomerDataService(int totalCustomerCount)
        {
            _customerList = new Lazy<IAsyncEnumerable<ImportCustomer>>(GetCustomerList(totalCustomerCount));
        }

        public async IAsyncEnumerable<ImportCustomer> GetCustomersFromImportAsync()
        {
            await foreach (var customer in _customerList.Value)
            {
                yield return customer;
            }
        }

        private async IAsyncEnumerable<ImportCustomer> GetCustomerList(int customerCount)
        {
            await Task.CompletedTask;

            for (int i = 0; i < customerCount; i++)
            {
                yield return new ImportCustomer()
                {
                    Id = (i + 1).ToString(),
                    FirstName = Guid.NewGuid().ToString().Replace("-", ""),
                    LastName = Guid.NewGuid().ToString().Replace("-", ""),
                    CellPhone = RandomPhone(),
                    Email = $"{Guid.NewGuid().ToString().Replace("-", "")}.{Guid.NewGuid().ToString().Replace("-", "")}@gmail.com"
                };
            }
        }

        public static string RandomPhone()
        {
            var random = new Random();
            string s = string.Empty;
            for (int i = 0; i < 10; i++)
                s = String.Concat(s, random.Next(1, 10).ToString());

            return s;
        }
    }
}
