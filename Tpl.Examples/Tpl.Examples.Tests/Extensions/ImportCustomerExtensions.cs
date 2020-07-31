using System;
using System.Collections.Generic;
using System.Text;
using Tpl.Examples.Tests.Models;

namespace Tpl.Examples.Tests.Extensions
{
    public static class ImportCustomerExtensions
    {
        public static Customer ToCustomer(this ImportCustomer importCustomer)
        {
            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                ExternalId = importCustomer.Id,
                Name = {FirstName = importCustomer.FirstName, LastName = importCustomer.LastName}
            };

            if (!string.IsNullOrWhiteSpace(importCustomer.Email))
            {
                customer.Emails.Add(new CustomerEmail()
                {
                    Type = "Primary",
                    Value = importCustomer.Email
                });
            }

            if (!string.IsNullOrWhiteSpace(importCustomer.CellPhone))
            {
                customer.Phones.Add(new CustomerPhone()
                {
                    Type = "Cell",
                    Value = importCustomer.CellPhone
                });
            }

            if (!string.IsNullOrWhiteSpace(importCustomer.DayPhone))
            {
                customer.Phones.Add(new CustomerPhone()
                {
                    Type = "Day",
                    Value = importCustomer.DayPhone
                });
            }

            if (!string.IsNullOrWhiteSpace(importCustomer.EvePhone))
            {
                customer.Phones.Add(new CustomerPhone()
                {
                    Type = "Evening",
                    Value = importCustomer.EvePhone
                });
            }

            return customer;
        }
    }
}
