// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConcurrencyLimiterSample
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddStackQueue((options) => {
                options.MaxConcurrentRequests = 12;
                options.RequestQueueLimit = 50;
            });
        }

        Random rnd = new Random();

        public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            app.UseConcurrencyLimiter();
            app.Run(async context =>
            {
                var delay = rnd.Next(15, 85);
                //var delay = 100;
                Task.Delay(delay).Wait();

                await context.Response.WriteAsync("Hello Request Throttling!");
            });
        }

        public static void Main(string[] args)
        {
            var host = new WebHostBuilder()
                .UseKestrel()
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }
}
