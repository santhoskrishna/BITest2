// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.RateLimiting;

/// <summary>
/// Specifies options for the rate limiting middleware.
/// </summary>
public sealed class RateLimiterOptions
{
    // Stores all of the keys for each partition so that we reuse the same objects.
    internal IDictionary<string, ISet<DefaultKeyType>> _partitionKeys = new Dictionary<string, ISet<DefaultKeyType>>();

    internal IDictionary<string, DefaultRateLimiterPolicy> PolicyMap { get; }
        = new Dictionary<string, DefaultRateLimiterPolicy>(StringComparer.Ordinal);

    internal IDictionary<string, Func<IServiceProvider, DefaultRateLimiterPolicy>> UnactivatedPolicyMap { get; }
        = new Dictionary<string, Func<IServiceProvider, DefaultRateLimiterPolicy>>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the global <see cref="PartitionedRateLimiter{HttpContext}"/> that will be applied on all requests.
    /// The global limiter will be executed first, followed by the endpoint-specific limiter, if one exists.
    /// </summary>
    public PartitionedRateLimiter<HttpContext>? GlobalLimiter { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="Func{OnRejectedContext, CancellationToken, ValueTask}"/> that handles requests rejected by this middleware.
    /// </summary>
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get; set; }

    /// <summary>
    /// Gets or sets the default status code to set on the response when a request is rejected.
    /// Defaults to <see cref="StatusCodes.Status503ServiceUnavailable"/>.
    /// </summary>
    /// <remarks>
    /// This status code will be set before <see cref="OnRejected"/> is called, so any status code set by
    /// <see cref="OnRejected"/> will "win" over this default.
    /// </remarks>
    public int RejectionStatusCode { get; set; } = StatusCodes.Status503ServiceUnavailable;

    /// <summary>
    /// Adds a new rate limiting policy with the given <paramref name="policyName"/>
    /// </summary>
    /// <param name="policyName">The name to be associated with the given <see cref="RateLimiter"/>.</param>
    /// <param name="partitioner">Method called every time an Acquire or WaitAsync call is made to determine what rate limiter to apply to the request.</param>
    public RateLimiterOptions AddPolicy<TPartitionKey>(string policyName, Func<HttpContext, RateLimitPartition<TPartitionKey>> partitioner)
    {
        ArgumentNullException.ThrowIfNull(policyName);
        ArgumentNullException.ThrowIfNull(partitioner);

        if (PolicyMap.ContainsKey(policyName) || UnactivatedPolicyMap.ContainsKey(policyName))
        {
            throw new ArgumentException($"There already exists a policy with the name {policyName}.", nameof(policyName));
        }

        PolicyMap.Add(policyName, new DefaultRateLimiterPolicy(ConvertPartitioner<TPartitionKey>(policyName, partitioner), null));

        return this;
    }

    /// <summary>
    /// Adds a new rate limiting policy with the given policyName.
    /// </summary>
    /// <param name="policyName">The name to be associated with the given TPolicy.</param>
    public RateLimiterOptions AddPolicy<TPartitionKey, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPolicy>(string policyName) where TPolicy : IRateLimiterPolicy<TPartitionKey>
    {
        ArgumentNullException.ThrowIfNull(policyName);

        if (PolicyMap.ContainsKey(policyName) || UnactivatedPolicyMap.ContainsKey(policyName))
        {
            throw new ArgumentException($"There already exists a policy with the name {policyName}.", nameof(policyName));
        }

        var policyType = new PolicyTypeState(typeof(TPolicy));
        Func<IServiceProvider, DefaultRateLimiterPolicy> policyFunc = serviceProvider =>
        {
            var instance = (IRateLimiterPolicy<TPartitionKey>)ActivatorUtilities.CreateInstance(serviceProvider, policyType.PolicyType);
            return new DefaultRateLimiterPolicy(ConvertPartitioner<TPartitionKey>(policyName, instance.GetPartition), instance.OnRejected);
        };

        UnactivatedPolicyMap.Add(policyName, policyFunc);

        return this;
    }

    /// <summary>
    /// Adds a new rate limiting policy with the given policyName.
    /// </summary>
    /// <param name="policyName">The name to be associated with the given <see cref="IRateLimiterPolicy{TPartitionKey}"/>.</param>
    /// <param name="policy">The <see cref="IRateLimiterPolicy{TPartitionKey}"/> to be applied.</param>
    public RateLimiterOptions AddPolicy<TPartitionKey>(string policyName, IRateLimiterPolicy<TPartitionKey> policy)
    {
        ArgumentNullException.ThrowIfNull(policyName);

        if (PolicyMap.ContainsKey(policyName) || UnactivatedPolicyMap.ContainsKey(policyName))
        {
            throw new ArgumentException($"There already exists a policy with the name {policyName}.", nameof(policyName));
        }

        ArgumentNullException.ThrowIfNull(policy);

        PolicyMap.Add(policyName, new DefaultRateLimiterPolicy(ConvertPartitioner<TPartitionKey>(policyName, policy.GetPartition), policy.OnRejected));

        return this;
    }

    // Converts a Partition<TKey> to a Partition<DefaultKeyType<TKey>> to prevent accidental collisions with the keys we create in the the RateLimiterOptionsExtensions.
    private Func<HttpContext, RateLimitPartition<DefaultKeyType>> ConvertPartitioner<TPartitionKey>(string policyName, Func<HttpContext, RateLimitPartition<TPartitionKey>> partitioner)
    {
        return (context =>
        {
            RateLimitPartition<TPartitionKey> partition = partitioner(context);
            // If we've already created this key for this policy, re-use it. DefaultKeyType uses reference equality, so we can't re-create the key on every call to the partitioner.
            bool found = false;
            DefaultKeyType partitionKey = new DefaultKeyType<TPartitionKey>(partition.PartitionKey);
            ISet<DefaultKeyType>? keys;

            if (!_partitionKeys.TryGetValue(policyName, out keys))
            {
                keys = new HashSet<DefaultKeyType>();
                _partitionKeys.Add(policyName, keys);
            }

            foreach (var k in keys)
            {
                if (k.GetKey()!.Equals(partition.PartitionKey))
                {
                    partitionKey = (DefaultKeyType)k;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                keys.Add(partitionKey);
            }
            return new RateLimitPartition<DefaultKeyType>(partitionKey, key => partition.Factory(partition.PartitionKey));
        });
    }

    // Workaround for linker bug: https://github.com/dotnet/linker/issues/1981
    private readonly struct PolicyTypeState
    {
        public PolicyTypeState([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type policyType)
        {
            PolicyType = policyType;
        }

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        public Type PolicyType { get; }
    }
}
