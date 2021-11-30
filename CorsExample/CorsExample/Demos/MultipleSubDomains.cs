using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace CorsExample.Demos
{
    public static class MultipleSubDomains
    {
        public static void AddMultipleSubDomains(this IServiceCollection services)
        {
            services.AddCors(opt =>
            {
                opt.AddPolicy(name: "SubDomainsPolicy", builder =>
                {
                    builder.WithOrigins("https://*.mydomain.com")
                        .SetIsOriginAllowedToAllowWildcardSubdomains();
                });
            });
        }
    }
}
