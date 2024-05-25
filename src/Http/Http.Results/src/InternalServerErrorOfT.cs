// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Http.HttpResults;

/// <summary>
/// An <see cref="IResult"/> that on execution will write an object to the response
/// with Internal Server Error (500) status code.
/// </summary>
/// <typeparam name="TValue">The type of error object that will be JSON serialized to the response body.</typeparam>
public sealed class InternalServerError<TValue> : IResult, IEndpointMetadataProvider, IStatusCodeHttpResult, IValueHttpResult, IValueHttpResult<TValue>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InternalServerError"/> class with the values
    /// provided.
    /// </summary>
    /// <param name="error">The error content to format in the entity body.</param>
    /// <param name="contentType">The content type (MIME type).</param>
    /// <param name="contentEncoding">The content encoding.</param>
    internal InternalServerError(TValue? error, string? contentType = null, Encoding? contentEncoding = null)
    {
        Value = error;
        ContentType = contentType;
        ContentEncoding = contentEncoding;
        HttpResultsHelper.ApplyProblemDetailsDefaultsIfNeeded(Value, StatusCode);
    }

    /// <summary>
    /// Gets the object result.
    /// </summary>
    public TValue? Value { get; }

    object? IValueHttpResult.Value => Value;

    /// <summary>
    /// Gets the content type.
    /// </summary>
    public string? ContentType { get; }

    /// <summary>
    /// Gets the content encoding.
    /// </summary>
    public Encoding? ContentEncoding { get; }

    /// <summary>
    /// Gets the HTTP status code: <see cref="StatusCodes.Status500InternalServerError"/>
    /// </summary>
    public int StatusCode => StatusCodes.Status500InternalServerError;

    int? IStatusCodeHttpResult.StatusCode => StatusCode;

    /// <inheritdoc/>
    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // Creating the logger with a string to preserve the category after the refactoring.
        var loggerFactory = httpContext.RequestServices.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("Microsoft.AspNetCore.Http.Result.InternalServerErrorObjectResult");

        HttpResultsHelper.Log.WritingResultAsStatusCode(logger, StatusCode);
        httpContext.Response.StatusCode = StatusCode;
        
        MediaTypeHeaderValue? mediaTypeHeaderValue = null;
        if (ContentType is not null)
        {
            mediaTypeHeaderValue = MediaTypeHeaderValue.Parse(ContentType);
            mediaTypeHeaderValue.Encoding = ContentEncoding ?? mediaTypeHeaderValue.Encoding;
        }

        return HttpResultsHelper.WriteResultAsJsonAsync(
                httpContext,
                logger: logger,
                value: Value,
                contentType: mediaTypeHeaderValue?.ToString());
    }

    /// <inheritdoc/>
    static void IEndpointMetadataProvider.PopulateMetadata(MethodInfo method, EndpointBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(builder);

        builder.Metadata.Add(new ProducesResponseTypeMetadata(StatusCodes.Status500InternalServerError, typeof(TValue), new[] { "application/json" }));
    }
}
