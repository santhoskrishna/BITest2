// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Http;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// An <see cref="IResult"/> that on execution will write an object to the response
/// with Unprocessable Entity (422) status code.
/// </summary>
public sealed class UnprocessableEntityObjectHttpResult : IResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnprocessableEntityObjectHttpResult"/> class with the values
    /// provided.
    /// </summary>
    /// <param name="error">The value to format in the entity body.</param>
    public UnprocessableEntityObjectHttpResult(object? error = null)
    {
        Value = error;
        HttpResultsHelper.ApplyProblemDetailsDefaultsIfNeeded(Value, StatusCode);
    }

    /// <summary>
    /// Gets an instance of <see cref="UnprocessableEntityObjectHttpResult"/> with a <c>null</c> <see cref="Value"/>.
    /// </summary>
    public static UnprocessableEntityObjectHttpResult Instance { get; } = new();

    /// <inheritdoc />
    public object? Value { get; internal init; }

    /// <inheritdoc />>
    public int StatusCode => StatusCodes.Status422UnprocessableEntity;

    /// <inheritdoc />
    public Task ExecuteAsync(HttpContext httpContext)
    {
        // Creating the logger with a string to preserve the category after the refactoring.
        var loggerFactory = httpContext.RequestServices.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("Microsoft.AspNetCore.Http.Result.UnprocessableEntityObjectResult");

        return HttpResultsHelper.WriteResultAsJsonAsync(
                httpContext,
                logger: logger,
                Value,
                StatusCode);
    }
}
