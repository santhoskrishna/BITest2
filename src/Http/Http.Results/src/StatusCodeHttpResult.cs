// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Represents an <see cref="IResult"/> that when executed will
/// produce an HTTP response with the given response status code.
/// </summary>
public partial class StatusCodeHttpResult : IResult
{
    internal StatusCodeHttpResult()
       : this(StatusCodes.Status200OK)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusCodeHttpResult"/> class
    /// with the given <paramref name="statusCode"/>.
    /// </summary>
    /// <param name="statusCode">The HTTP status code of the response.</param>
    internal StatusCodeHttpResult(int statusCode)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// Gets or sets the HTTP status code.
    /// </summary>
    public int StatusCode { get; internal init; }

    /// <summary>
    /// Sets the status code on the HTTP response.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <returns>A task that represents the asynchronous execute operation.</returns>
    public Task ExecuteAsync(HttpContext httpContext)
    {
        var factory = httpContext.RequestServices.GetRequiredService<ILoggerFactory>();
        var logger = factory.CreateLogger(GetType());

        Log.StatusCodeResultExecuting(logger, StatusCode);
        httpContext.Response.StatusCode = StatusCode;

        return WriteContentAsync(httpContext);
    }

    internal virtual Task WriteContentAsync(HttpContext httpContext)
    {
        return Task.CompletedTask;
    }

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Information,
            "Executing StatusCodeResult, setting HTTP status code {StatusCode}.",
            EventName = "StatusCodeResultExecuting")]
        public static partial void StatusCodeResultExecuting(ILogger logger, int statusCode);
    }
}
