using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tpl.Examples.Tests.Models;

namespace Tpl.Examples.Tests.Services
{
    public class CustomerService
    {
        public async Task<Customer> SaveCustomer(Customer customer)
        {
            await Task.Delay(100);
            return await Task.FromResult(CreateCustomer(customer));
        }

        public async IAsyncEnumerable<Customer> SaveCustomers(List<Customer> customers)
        {
            if (customers.Count > 100)
            {
                throw new ArgumentException("Too many customers.", nameof(customers));
            }

            await Task.Delay(customers.Count);

            foreach (var customer in customers)
            {
                yield return CreateCustomer(customer);
            }
        }

        private static Customer CreateCustomer(Customer customer)
        {
            return new Customer()
            {
                Id = Guid.NewGuid(),
                ExternalId = customer.ExternalId,
                Name = customer.Name,
                Emails = customer.Emails,
                Phones = customer.Phones
            };
        }
    }
}
