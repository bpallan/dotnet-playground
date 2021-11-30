using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace CorsExample.Demos
{
    public static class MultipleNamedPoliciesForAttributes
    {
        public static void AddMultipleNamedPoliciesForAttributes(this IServiceCollection services)
        {
            services.AddCors(opt =>
            {
                opt.AddPolicy(name: "CorsPolicy1", builder =>
                {
                    builder.AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
                opt.AddPolicy(name: "CorsPolicy2", builder =>
                {
                    builder.AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });
        }
    }
}
