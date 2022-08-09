// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.OutputCaching.Tests;

public class OutputCachePolicyBuilderTests
{
    [Fact]
    public void BuildPolicy_CreatesDefaultPolicy()
    {
        var builder = new OutputCachePolicyBuilder();
        var policy = builder.Build();

        Assert.Equal(DefaultPolicy.Instance, policy);
    }

    [Fact]
    public async Task BuildPolicy_CreatesExpirePolicy()
    {
        var context = CreateTestContext();
        var duration = 42;

        var builder = new OutputCachePolicyBuilder();
        var policy = builder.Expire(TimeSpan.FromSeconds(duration)).Build();
        await policy.CacheRequestAsync(context, cancellation: default);

        Assert.True(context.EnableOutputCaching);
        Assert.Equal(duration, context.ResponseExpirationTimeSpan?.TotalSeconds);
    }

    [Fact]
    public async Task BuildPolicy_CreatesNoStorePolicy()
    {
        var context = CreateTestContext();

        var builder = new OutputCachePolicyBuilder();
        var policy = builder.NoCache().Build();
        await policy.CacheRequestAsync(context, cancellation: default);

        Assert.False(context.EnableOutputCaching);
    }

    [Fact]
    public async Task BuildPolicy_AddsCustomPolicy()
    {
        var options = new OutputCacheOptions();
        var name = "MyPolicy";
        var duration = 42;
        options.AddPolicy(name, b => b.Expire(TimeSpan.FromSeconds(duration)));

        var context = CreateTestContext(options: options);

        var builder = new OutputCachePolicyBuilder();
        var policy = builder.AddPolicy(new NamedPolicy(name)).Build();
        await policy.CacheRequestAsync(context, cancellation: default);

        Assert.True(context.EnableOutputCaching);
        Assert.Equal(duration, context.ResponseExpirationTimeSpan?.TotalSeconds);
    }

    [Fact]
    public async Task BuildPolicy_CreatesVaryByHeaderPolicy()
    {
        var context = CreateTestContext();
        context.HttpContext.Request.Headers["HeaderA"] = "ValueA";
        context.HttpContext.Request.Headers["HeaderB"] = "ValueB";

        var builder = new OutputCachePolicyBuilder();
        var policy = builder.VaryByHeader("HeaderA", "HeaderC").Build();
        await policy.CacheRequestAsync(context, cancellation: default);

        Assert.True(context.EnableOutputCaching);
        Assert.Contains("HeaderA", (IEnumerable<string>)context.CacheVaryByRules.HeaderNames);
        Assert.Contains("HeaderC", (IEnumerable<string>)context.CacheVaryByRules.HeaderNames);
        Assert.DoesNotContain("HeaderB", (IEnumerable<string>)context.CacheVaryByRules.HeaderNames);
    }

    [Fact]
    public async Task BuildPolicy_CreatesVaryByQueryPolicy()
    {
        var context = CreateTestContext();
        context.HttpContext.Request.QueryString = new QueryString("?QueryA=ValueA&QueryB=ValueB");

        var builder = new OutputCachePolicyBuilder();
        var policy = builder.VaryByQuery("QueryA", "QueryC").Build();
        await policy.CacheRequestAsync(context, cancellation: default);

        Assert.True(context.EnableOutputCaching);
        Assert.Contains("QueryA", (IEnumerable<string>)context.CacheVaryByRules.QueryKeys);
        Assert.Contains("QueryC", (IEnumerable<string>)context.CacheVaryByRules.QueryKeys);
        Assert.DoesNotContain("QueryB", (IEnumerable<string>)context.CacheVaryByRules.QueryKeys);
    }

    [Fact]
    public async Task BuildPolicy_CreatesVaryByRoutePolicy()
    {
        var context = CreateTestContext();
        context.HttpContext.Request.RouteValues = new Routing.RouteValueDictionary()
        {
            ["RouteA"] = "ValueA",
            ["RouteB"] = 123.456,
        };

        var builder = new OutputCachePolicyBuilder();
        var policy = builder.VaryByRouteValue("RouteA", "RouteC").Build();
        await policy.CacheRequestAsync(context, cancellation: default);

        Assert.True(context.EnableOutputCaching);
        Assert.Contains("RouteA", (IEnumerable<string>)context.CacheVaryByRules.RouteValues);
        Assert.Contains("RouteC", (IEnumerable<string>)context.CacheVaryByRules.RouteValues);
        Assert.DoesNotContain("RouteB", (IEnumerable<string>)context.CacheVaryByRules.RouteValues);
    }

    [Fact]
    public async Task BuildPolicy_CreatesVaryByValuePolicy()
    {
        var context = CreateTestContext();

        var builder = new OutputCachePolicyBuilder();
        var policy = builder.VaryByValue(context => new KeyValuePair<string, string>("color", "blue")).Build();
        await policy.CacheRequestAsync(context, cancellation: default);

        Assert.True(context.EnableOutputCaching);
        Assert.Equal("blue", context.CacheVaryByRules.VaryByCustom["color"]);
    }

    [Fact]
    public async Task BuildPolicy_CreatesTagPolicy()
    {
        var context = CreateTestContext();

        var builder = new OutputCachePolicyBuilder();
        var policy = builder.Tag("tag1", "tag2").Build();
        await policy.CacheRequestAsync(context, cancellation: default);

        Assert.True(context.EnableOutputCaching);
        Assert.Contains("tag1", context.Tags);
        Assert.Contains("tag2", context.Tags);
    }

    [Fact]
    public async Task BuildPolicy_AllowsLocking()
    {
        var context = CreateTestContext();

        var builder = new OutputCachePolicyBuilder();
        var policy = builder.Build();
        await policy.CacheRequestAsync(context, cancellation: default);

        Assert.True(context.AllowLocking);
    }

    [Fact]
    public async Task BuildPolicy_EnablesLocking()
    {
        var cache = new TestOutputCache();
        var context = TestUtils.CreateTestContext(cache);

        var builder = new OutputCachePolicyBuilder();
        var policy = builder.AllowLocking(true).Build();
        await policy.CacheRequestAsync(context, cancellation: default);

        Assert.True(context.AllowLocking);
    }

    [Fact]
    public async Task BuildPolicy_DisablesLocking()
    {
        var context = CreateTestContext();

        var builder = new OutputCachePolicyBuilder();
        var policy = builder.AllowLocking(false).Build();
        await policy.CacheRequestAsync(context, cancellation: default);

        Assert.False(context.AllowLocking);
    }

    [Fact]
    public async Task BuildPolicy_ClearsDefaultPolicy()
    {
        var context = CreateTestContext();

        var builder = new OutputCachePolicyBuilder();
        var policy = builder.Clear().Build();
        await policy.CacheRequestAsync(context, cancellation: default);

        Assert.False(context.AllowLocking);
        Assert.False(context.AllowCacheLookup);
        Assert.False(context.AllowCacheStorage);
        Assert.False(context.EnableOutputCaching);
    }

    [Fact]
    public async Task BuildPolicy_DisablesCache()
    {
        var context = CreateTestContext();

        var builder = new OutputCachePolicyBuilder();
        var policy = builder.NoCache().Build();
        await policy.CacheRequestAsync(context, cancellation: default);

        Assert.False(context.EnableOutputCaching);
    }

    [Fact]
    public async Task BuildPolicy_EnablesCache()
    {
        var context = CreateTestContext();

        var builder = new OutputCachePolicyBuilder();
        var policy = builder.NoCache().Cache().Build();
        await policy.CacheRequestAsync(context, cancellation: default);

        Assert.True(context.EnableOutputCaching);
    }

    private static OutputCacheContext CreateTestContext(IOutputCacheStore? cache = null, OutputCacheOptions? options = null)
    {
        return new OutputCacheContext(new DefaultHttpContext(), cache ?? new TestOutputCache(), options ?? Options.Create(new OutputCacheOptions()).Value, NullLogger.Instance)
        {
        };
    }
}
