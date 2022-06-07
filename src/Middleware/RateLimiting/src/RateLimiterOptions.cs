// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.RateLimiting;

/// <summary>
/// Specifies options for the rate limiting middleware.
/// </summary>
public sealed class RateLimiterOptions
{
    private PartitionedRateLimiter<HttpContext> _limiter = new NoLimiter<HttpContext>();
    private Func<HttpContext, RateLimitLease, Task> _onRejected = (context, lease) =>
    {
        return Task.CompletedTask;
    };
    private IDictionary<string, RateLimiterPolicy> PartitionMap { get; }
        = new Dictionary<string, RateLimiterPolicy>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the <see cref="PartitionedRateLimiter{TResource}"/>
    /// </summary>
    public PartitionedRateLimiter<HttpContext> Limiter
    {
        get => _limiter;
        set => _limiter = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or sets a <see cref="Func{HttpContext, RateLimitLease, Task}"/> that handles requests rejected by this middleware.
    /// </summary>
    public Func<HttpContext, RateLimitLease, Task> OnRejected
    {
        get => _onRejected;
        set => _onRejected = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or sets the default status code to set on the response when a request is rejected.
    /// Defaults to <see cref="StatusCodes.Status503ServiceUnavailable"/>.
    /// </summary>
    /// <remarks>
    /// This status code will be set before <see cref="OnRejected"/> is called, so any status code set by
    /// <see cref="OnRejected"/> will "win" over this default.
    /// </remarks>
    public int DefaultRejectionStatusCode { get; set; } = StatusCodes.Status503ServiceUnavailable;

    /// <summary>
    /// Adds a new rate limiting policy with the given name.
    /// </summary>
    /// <param name="name">The name to be associated with the given <see cref="RateLimiter"/></param>
    /// <param name="partitioner">Method called every time an Acquire or WaitAsync call is made to figure out what rate limiter to apply to the request.</param>
    /// <param name="global">Determines if this policy should be shared across endpoints. Defaults to false.</param>
    public RateLimiterOptions AddPolicy<TKey>(string name, Func<HttpContext, RateLimitPartition<TKey>> partitioner, bool global = false)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (partitioner == null)
        {
            throw new ArgumentNullException(nameof(partitioner));
        }

        if (PartitionMap.ContainsKey(name))
        {
            throw new ArgumentException("There already exists a partition with the name {name}");
        }

        PartitionMap.Add(name, partitioner);

        return this;
    }
}
