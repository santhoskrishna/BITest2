// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Http;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Represents an <see cref="IResult"/> that when executed will
/// produce an HTTP response with the given response status code.
/// </summary>
public sealed partial class StatusCodeHttpResult : IResult
{
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
    /// Gets the HTTP status code.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Sets the status code on the HTTP response.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <returns>A task that represents the asynchronous execute operation.</returns>
    public Task ExecuteAsync(HttpContext httpContext)
    {
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<StatusCodeHttpResult>>();
        HttpResultsHelper.Log.WritingResultAsStatusCode(logger, StatusCode);

        httpContext.Response.StatusCode = StatusCode;

        return Task.CompletedTask;
    }
}
