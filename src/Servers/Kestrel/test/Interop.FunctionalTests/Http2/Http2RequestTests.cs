// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Internal;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;
using Microsoft.AspNetCore.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Interop.FunctionalTests.Http2;

[Collection(nameof(NoParallelCollection))]
public class Http2RequestTests : LoggedTest
{
    [Fact]
    public async Task GET_Metrics_HttpProtocolAndTlsSet()
    {
        // Arrange
        var protocolTcs = new TaskCompletionSource<SslProtocols>(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = CreateHostBuilder(c =>
        {
            protocolTcs.SetResult(c.Features.Get<ISslStreamFeature>().SslStream.SslProtocol);
            return Task.CompletedTask;
        }, protocol: HttpProtocols.Http2, plaintext: false);

        using (var host = builder.Build())
        {
            var meterFactory = host.Services.GetRequiredService<IMeterFactory>();

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var connectionDuration = new InstrumentRecorder<double>(meterFactory, "Microsoft.AspNetCore.Server.Kestrel", "kestrel-connection-duration");
            using var measurementReporter = new MeasurementReporter<double>(meterFactory, "Microsoft.AspNetCore.Server.Kestrel", "kestrel-connection-duration");
            measurementReporter.Register(m =>
            {
                tcs.SetResult();
            });

            await host.StartAsync();
            var client = HttpHelpers.CreateClient();

            // Act
            var request1 = new HttpRequestMessage(HttpMethod.Get, $"https://127.0.0.1:{host.GetPort()}/");
            request1.Version = HttpVersion.Version20;
            request1.VersionPolicy = HttpVersionPolicy.RequestVersionExact;

            var response1 = await client.SendAsync(request1, CancellationToken.None);
            response1.EnsureSuccessStatusCode();

            var protocol = await protocolTcs.Task.DefaultTimeout();

            // Dispose the client to end the connection.
            client.Dispose();
            // Wait for measurement to be available.
            await tcs.Task.DefaultTimeout();

            // Assert
            Assert.Collection(connectionDuration.GetMeasurements(),
                m =>
                {
                    Assert.True(m.Value > 0);
                    Assert.Equal(protocol.ToString(), m.Tags.ToArray().Single(t => t.Key == "tls-protocol").Value);
                    Assert.Equal("HTTP/2", m.Tags.ToArray().Single(t => t.Key == "http-protocol").Value);
                    Assert.Equal($"127.0.0.1:{host.GetPort()}", m.Tags.ToArray().Single(t => t.Key == "endpoint").Value);
                });

            await host.StopAsync();
        }
    }

    [Fact]
    public async Task GET_NoTLS_Http11RequestToHttp2Endpoint_400Result()
    {
        // Arrange
        var builder = CreateHostBuilder(c => Task.CompletedTask, protocol: HttpProtocols.Http2, plaintext: true);

        using (var host = builder.Build())
        using (var client = HttpHelpers.CreateClient())
        {
            await host.StartAsync().DefaultTimeout();

            var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{host.GetPort()}/");
            request.Version = HttpVersion.Version11;
            request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;

            // Act
            var responseMessage = await client.SendAsync(request, CancellationToken.None).DefaultTimeout();

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, responseMessage.StatusCode);
            Assert.Equal("An HTTP/1.x request was sent to an HTTP/2 only endpoint.", await responseMessage.Content.ReadAsStringAsync());
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [QuarantinedTest("https://github.com/dotnet/aspnetcore/issues/41074")]
    public async Task GET_RequestReturnsLargeData_GracefulShutdownDuringRequest_RequestGracefullyCompletes(bool hasTrailers)
    {
        // Enable client logging.
        // Test failure on CI could be from HttpClient bug.
        using var httpEventSource = new HttpEventSourceListener(LoggerFactory);

        // Arrange
        const int DataLength = 500_000;
        var randomBytes = Enumerable.Range(1, DataLength).Select(i => (byte)((i % 10) + 48)).ToArray();

        var syncPoint = new SyncPoint();

        ILogger logger = null;
        var builder = CreateHostBuilder(
            async c =>
            {
                await syncPoint.WaitToContinue();

                var memory = c.Response.BodyWriter.GetMemory(randomBytes.Length);

                logger.LogInformation($"Server writing {randomBytes.Length} bytes response");
                randomBytes.CopyTo(memory);

                // It's important for this test that the large write is the last data written to
                // the response and it's not awaited by the request delegate.
                logger.LogInformation($"Server advancing {randomBytes.Length} bytes response");
                c.Response.BodyWriter.Advance(randomBytes.Length);

                if (hasTrailers)
                {
                    c.Response.AppendTrailer("test-trailer", "value!");
                }
            },
            protocol: HttpProtocols.Http2,
            plaintext: true);

        using var host = builder.Build();
        logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Test");

        var client = HttpHelpers.CreateClient();

        // Act
        await host.StartAsync().DefaultTimeout();

        var longRunningTask = StartLongRunningRequestAsync(logger, host, client);

        logger.LogInformation("Waiting for request on server");
        await syncPoint.WaitForSyncPoint().DefaultTimeout();

        logger.LogInformation("Stopping server");
        var stopTask = host.StopAsync();

        syncPoint.Continue();

        var (readData, trailers) = await longRunningTask.DefaultTimeout();
        await stopTask.DefaultTimeout();

        // Assert
        Assert.Equal(randomBytes, readData);
        if (hasTrailers)
        {
            Assert.Equal("value!", trailers.GetValues("test-trailer").Single());
        }
    }

    private static async Task<(byte[], HttpResponseHeaders)> StartLongRunningRequestAsync(ILogger logger, IHost host, HttpMessageInvoker client)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{host.GetPort()}/");
        request.Headers.Host = "localhost2";
        request.Version = HttpVersion.Version20;
        request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;

        logger.LogInformation($"Sending request to '{request.RequestUri}'.");
        var responseMessage = await client.SendAsync(request, CancellationToken.None).DefaultTimeout();
        responseMessage.EnsureSuccessStatusCode();

        var responseStream = await responseMessage.Content.ReadAsStreamAsync();

        logger.LogInformation($"Started reading response content");
        var data = new List<byte>();
        var buffer = new byte[1024 * 128];
        int readCount;
        try
        {
            while ((readCount = await responseStream.ReadAsync(buffer)) != 0)
            {
                data.AddRange(buffer.AsMemory(0, readCount).ToArray());
                logger.LogInformation($"Received {readCount} bytes. Total {data.Count} bytes.");
            }
        }
        catch
        {
            logger.LogInformation($"Error reading response. Total {data.Count} bytes.");

            throw;
        }
        logger.LogInformation($"Finished reading response content");

        return (data.ToArray(), responseMessage.TrailingHeaders);
    }

    private IHostBuilder CreateHostBuilder(RequestDelegate requestDelegate, HttpProtocols? protocol = null, Action<KestrelServerOptions> configureKestrel = null, bool? plaintext = null)
    {
        return HttpHelpers.CreateHostBuilder(AddTestLogging, requestDelegate, protocol, configureKestrel, plaintext);
    }
}
