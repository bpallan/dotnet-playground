using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace CorsExample.Demos
{
    public static class RequireSpecificOrigin
    {
        public static void AddSpecificOriginRequirement(this IServiceCollection services)
        {
            services.AddCors(opt =>
            {
                opt.AddPolicy(name: "SpecificOriginPolicy", builder =>
                {
                    builder.WithOrigins("https://localhost:5011")
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });
        }
    }
}
