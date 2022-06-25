// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Options for <see cref="StatusCodePagesMiddleware"/>.
/// </summary>
public class StatusCodePagesOptions
{
    /// <summary>
    /// Creates a default <see cref="StatusCodePagesOptions"/> which produces a plaintext response
    /// containing the status code and the reason phrase.
    /// </summary>
    public StatusCodePagesOptions()
    {
        HandleAsync = async context =>
        {
            // TODO: Render with a pre-compiled html razor view.
            var statusCode = context.HttpContext.Response.StatusCode;

            if (context.HttpContext.RequestServices.GetService<IProblemDetailsService>() is { } problemDetailsService)
            {
                await problemDetailsService.WriteAsync(
                        context.HttpContext,
                        statusCode: statusCode);
            }

            if (!context.HttpContext.Response.HasStarted)
            {
                var body = BuildResponseBody(statusCode);

                context.HttpContext.Response.ContentType = "text/plain";
                await context.HttpContext.Response.WriteAsync(body);
            }
        };
    }

    private static string BuildResponseBody(int httpStatusCode)
    {
        // Note the 500 spaces are to work around an IE 'feature'
        var internetExplorerWorkaround = new string(' ', 500);

        var reasonPhrase = ReasonPhrases.GetReasonPhrase(httpStatusCode);

        return string.Format(CultureInfo.InvariantCulture, "Status Code: {0}{1}{2}{3}",
                                                                httpStatusCode,
                                                                string.IsNullOrWhiteSpace(reasonPhrase) ? "" : "; ",
                                                                reasonPhrase,
                                                                internetExplorerWorkaround);
    }

    /// <summary>
    /// The handler that generates the response body for the given <see cref="StatusCodeContext"/>. By default this produces a plain text response that includes the status code.
    /// </summary>
    public Func<StatusCodeContext, Task> HandleAsync { get; set; }
}
