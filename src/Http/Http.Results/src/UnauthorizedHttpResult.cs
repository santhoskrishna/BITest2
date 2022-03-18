// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Http;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Represents an <see cref="IResult"/> that when executed will
/// produce an HTTP response with the No Unauthorized (401) status code.
/// </summary>
public sealed class UnauthorizedHttpResult : IResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnauthorizedHttpResult"/> class.
    /// </summary>
    public UnauthorizedHttpResult()
    {
    }

    /// <inheritdoc />
    public int StatusCode => StatusCodes.Status401Unauthorized;

    /// <inheritdoc />
    public Task ExecuteAsync(HttpContext httpContext)
    {
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<UnauthorizedHttpResult>>();
        HttpResultsWriter.Log.WritingResultAsStatusCode(logger, StatusCode);

        httpContext.Response.StatusCode = StatusCode;

        return Task.CompletedTask;
    }

}
