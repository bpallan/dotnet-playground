using System;
using System.Collections.Generic;
using System.Text;

namespace Tpl.Examples.Tests.Models
{
    public class Customer
    {
        public Guid? Id { get; set; }
        public string ExternalId { get; set; }
        public CustomerName Name { get; set; } = new CustomerName();
        public List<CustomerEmail> Emails { get; set; } = new List<CustomerEmail>();
        public List<CustomerPhone> Phones { get; set; } = new List<CustomerPhone>();
    }
}
