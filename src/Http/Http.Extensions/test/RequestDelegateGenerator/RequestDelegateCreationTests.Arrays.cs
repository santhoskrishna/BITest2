// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http.Generators.StaticRouteHandlerModel;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore.Http.Generators.Tests;

public abstract partial class RequestDelegateCreationTests
{
    [Fact]
    public async Task MapAction_ExplicitQuery_ComplexTypeArrayParam()
    {
        var (results, compilation) = await RunGeneratorAsync("""
app.MapGet("/hello", ([FromQuery]Todo[] p) => p.Length);
""");
        var endpoint = GetEndpointFromCompilation(compilation);

        VerifyStaticEndpointModel(results, endpointModel =>
        {
            Assert.Equal("/hello", endpointModel.RoutePattern);
            Assert.Equal("MapGet", endpointModel.HttpMethod);
        });

        var httpContext = CreateHttpContext();
        httpContext.Request.QueryString = new QueryString("?p=1&p=1");

        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "2");
        await VerifyAgainstBaselineUsingFile(compilation);
    }

    [Fact]
    public async Task MapAction_ExplicitHeader_ComplexTypeArrayParam()
    {
        var (results, compilation) = await RunGeneratorAsync("""
app.MapGet("/hello", ([FromHeader]Todo[] p) => p.Length);
""");
        var endpoint = GetEndpointFromCompilation(compilation);

        VerifyStaticEndpointModel(results, endpointModel =>
        {
            Assert.Equal("/hello", endpointModel.RoutePattern);
            Assert.Equal("MapGet", endpointModel.HttpMethod);
        });

        var httpContext = CreateHttpContext();
        httpContext.Request.Headers.Add("p", new StringValues(new string[] { "1", "1" }));

        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "2");
        await VerifyAgainstBaselineUsingFile(compilation);
    }

    [Fact]
    public async Task MapAction_ExplicitHeader_StringArrayParam()
    {
        var (results, compilation) = await RunGeneratorAsync("""
app.MapGet("/hello", ([FromHeader]string[] p) => p.Length);
""");
        var endpoint = GetEndpointFromCompilation(compilation);

        VerifyStaticEndpointModel(results, endpointModel =>
        {
            Assert.Equal("/hello", endpointModel.RoutePattern);
            Assert.Equal("MapGet", endpointModel.HttpMethod);
        });

        var httpContext = CreateHttpContext();
        httpContext.Request.Headers.Add("p", new StringValues(new string[] { "1", "1" }));

        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "2");
        await VerifyAgainstBaselineUsingFile(compilation);
    }

    [Fact]
    public async Task MapAction_ExplicitHeader_NullableStringArrayParam()
    {
        var (results, compilation) = await RunGeneratorAsync("""
app.MapGet("/hello", ([FromHeader]string?[] p) => p.Length);
""");
        var endpoint = GetEndpointFromCompilation(compilation);

        VerifyStaticEndpointModel(results, endpointModel =>
        {
            Assert.Equal("/hello", endpointModel.RoutePattern);
            Assert.Equal("MapGet", endpointModel.HttpMethod);
        });

        var httpContext = CreateHttpContext();
        httpContext.Request.Headers.Add("p", new StringValues(new string[] { "1", "1" }));

        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "2");
        await VerifyAgainstBaselineUsingFile(compilation);
    }

    [Fact]
    public async Task MapAction_ImplicitQuery_ComplexTypeArrayParam()
    {
        var (results, compilation) = await RunGeneratorAsync("""
app.MapGet("/hello", (Todo[] p) => p.Length);
""");
        var endpoint = GetEndpointFromCompilation(compilation);

        VerifyStaticEndpointModel(results, endpointModel =>
        {
            Assert.Equal("/hello", endpointModel.RoutePattern);
            Assert.Equal("MapGet", endpointModel.HttpMethod);
        });

        var httpContext = CreateHttpContext();
        httpContext.Request.QueryString = new QueryString("?p=1&p=1");

        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "2");
        await VerifyAgainstBaselineUsingFile(compilation);
    }

    [Fact]
    public async Task MapAction_ExplicitQuery_StringArrayParam()
    {
        var (results, compilation) = await RunGeneratorAsync("""
app.MapGet("/hello", ([FromQuery]string[] p) => p.Length);
""");
        var endpoint = GetEndpointFromCompilation(compilation);

        VerifyStaticEndpointModel(results, endpointModel =>
        {
            Assert.Equal("/hello", endpointModel.RoutePattern);
            Assert.Equal("MapGet", endpointModel.HttpMethod);
        });

        var httpContext = CreateHttpContext();
        httpContext.Request.QueryString = new QueryString("?p=Item1&p=Item2");

        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "2");
        await VerifyAgainstBaselineUsingFile(compilation);
    }

    [Fact]
    public async Task MapAction_ImplicitQuery_StringArrayParam()
    {
        var (results, compilation) = await RunGeneratorAsync("""
app.MapGet("/hello", (string[] p) => p.Length);
""");
        var endpoint = GetEndpointFromCompilation(compilation);

        VerifyStaticEndpointModel(results, endpointModel =>
        {
            Assert.Equal("/hello", endpointModel.RoutePattern);
            Assert.Equal("MapGet", endpointModel.HttpMethod);
        });

        var httpContext = CreateHttpContext();
        httpContext.Request.QueryString = new QueryString("?p=Item1&p=Item2");

        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "2");
        await VerifyAgainstBaselineUsingFile(compilation);
    }

    [Fact]
    public async Task MapAction_ImplicitQuery_NullableStringArrayParam_QueryNotPresent()
    {
        var (results, compilation) = await RunGeneratorAsync("""
app.MapGet("/hello", (string?[] p) => p.Length);
""");
        var endpoint = GetEndpointFromCompilation(compilation);

        VerifyStaticEndpointModel(results, endpointModel =>
        {
            Assert.Equal("/hello", endpointModel.RoutePattern);
            Assert.Equal("MapGet", endpointModel.HttpMethod);
        });

        var httpContext = CreateHttpContext();

        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "0");
        await VerifyAgainstBaselineUsingFile(compilation);
    }

    [Fact]
    public async Task MapAction_ImplicitQuery_NullableStringArrayParam_EmptyQueryValues()
    {
        var (results, compilation) = await RunGeneratorAsync("""
app.MapGet("/hello", (string?[] p) => p.Length);
""");
        var endpoint = GetEndpointFromCompilation(compilation);

        VerifyStaticEndpointModel(results, endpointModel =>
        {
            Assert.Equal("/hello", endpointModel.RoutePattern);
            Assert.Equal("MapGet", endpointModel.HttpMethod);
        });

        var httpContext = CreateHttpContext();
        httpContext.Request.QueryString = new QueryString("?p=&p=");

        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "2");
        await VerifyAgainstBaselineUsingFile(compilation);
    }

    [Fact]
    public async Task MapAction_ExplicitQuery_NullableStringArrayParam()
    {
        var (results, compilation) = await RunGeneratorAsync("""
app.MapGet("/hello", ([FromQuery]string?[] p) => p.Length);
""");
        var endpoint = GetEndpointFromCompilation(compilation);

        VerifyStaticEndpointModel(results, endpointModel =>
        {
            Assert.Equal("/hello", endpointModel.RoutePattern);
            Assert.Equal("MapGet", endpointModel.HttpMethod);
        });

        var httpContext = CreateHttpContext();
        httpContext.Request.QueryString = new QueryString("?p=Item1&p=Item2");

        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "2");
        await VerifyAgainstBaselineUsingFile(compilation);
    }

    [Fact]
    public async Task MapAction_ImplicitQuery_NullableStringArrayParam()
    {
        var (results, compilation) = await RunGeneratorAsync("""
app.MapGet("/hello", (string?[] p) => p.Length);
""");
        var endpoint = GetEndpointFromCompilation(compilation);

        VerifyStaticEndpointModel(results, endpointModel =>
        {
            Assert.Equal("/hello", endpointModel.RoutePattern);
            Assert.Equal("MapGet", endpointModel.HttpMethod);
        });

        var httpContext = CreateHttpContext();
        httpContext.Request.QueryString = new QueryString("?p=Item1&p=Item2");

        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "2");
        await VerifyAgainstBaselineUsingFile(compilation);
    }
}
