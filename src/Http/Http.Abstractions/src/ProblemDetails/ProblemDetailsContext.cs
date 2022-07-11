// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Mvc;

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Represent the current problem detatils context for the request.
/// </summary>
public sealed class ProblemDetailsContext
{
    private ProblemDetails? _problemDetails;

    /// <summary>
    /// The <see cref="HttpContext"/> associated with the current request being processed by the filter.
    /// </summary>
    public required HttpContext HttpContext { get; init; }

    /// <summary>
    /// A collection of additional arbitrary metadata associated with the current request endpoint.
    /// </summary>
    public EndpointMetadataCollection? AdditionalMetadata { get; init; }

    /// <summary>
    /// A instance of <see cref="ProblemDetails"/> that will be
    /// used during the response payload generation.
    /// </summary>
    public ProblemDetails ProblemDetails
    {
        get => _problemDetails ??= new ProblemDetails();
        init => _problemDetails = value;
    }
}
