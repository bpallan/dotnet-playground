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

        public async Task<List<Customer>> SaveCustomers(List<Customer> customers)
        {
            var savedCustomers = new List<Customer>();

            if (customers.Count > 100)
            {
                throw new ArgumentException("Too many customers.", nameof(customers));
            }

            await Task.Delay(100);

            foreach (var customer in customers)
            {
                savedCustomers.Add(CreateCustomer(customer));
            }

            return savedCustomers;
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
