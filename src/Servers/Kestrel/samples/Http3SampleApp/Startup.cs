// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.WebTransport;
using Microsoft.AspNetCore.Server.Kestrel.Core.WebTransport;

namespace Http3SampleApp;

public class Startup
{
    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    public void ConfigureServices(IServiceCollection services)
    {
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            var feature = context.Features.Get<IHttpWebTransportSessionFeature>();
            if (feature is not null)
            {
                var session = await feature.AcceptAsync(CancellationToken.None);

                var stream = await session.AcceptStreamAsync(CancellationToken.None);

                // WRITE TO A STREAM
                await Task.Delay(200);
                await stream.WriteAsync(new ReadOnlyMemory<byte>(new byte[] { 65, 66, 67, 68, 69 }));
                await stream.FlushAsync();

                // READ FROM A STREAM:
                //var memory = new Memory<byte>(new byte[4096]);
                //var test = await stream.ReadAsync(memory);
                //Console.WriteLine(System.Text.ASCIIEncoding.Default.GetString(memory.ToArray()));
            }
            else
            {
                await next(context);
            }

            await Task.Delay(TimeSpan.FromMinutes(150));
        });
    }
}
