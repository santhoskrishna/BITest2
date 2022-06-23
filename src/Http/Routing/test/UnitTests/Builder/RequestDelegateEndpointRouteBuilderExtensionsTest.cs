// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.IO.Pipelines;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Microsoft.AspNetCore.Builder;

public class RequestDelegateEndpointRouteBuilderExtensionsTest
{
    private EndpointDataSource GetBuilderEndpointDataSource(IEndpointRouteBuilder endpointRouteBuilder) =>
        Assert.Single(endpointRouteBuilder.DataSources);

    private RouteEndpointBuilder GetRouteEndpointBuilder(IEndpointRouteBuilder endpointRouteBuilder) =>
        GetBuilderEndpointDataSource(endpointRouteBuilder) switch
        {
            RouteEndpointDataSource routeDataSource => routeDataSource.GetSingleRouteEndpointBuilder(),
            ModelEndpointDataSource modelDataSource => Assert.IsType<RouteEndpointBuilder>(Assert.Single(modelDataSource.EndpointBuilders)),
            _ => throw new InvalidOperationException($"Unknown EndointDataSource type!"),
        };

    public static object[][] MapMethods
    {
        get
        {
            IEndpointConventionBuilder MapGet(IEndpointRouteBuilder routes, string template, RequestDelegate action) =>
                routes.MapGet(template, action);

            IEndpointConventionBuilder MapPost(IEndpointRouteBuilder routes, string template, RequestDelegate action) =>
                routes.MapPost(template, action);

            IEndpointConventionBuilder MapPut(IEndpointRouteBuilder routes, string template, RequestDelegate action) =>
                routes.MapPut(template, action);

            IEndpointConventionBuilder MapDelete(IEndpointRouteBuilder routes, string template, RequestDelegate action) =>
                routes.MapDelete(template, action);

            IEndpointConventionBuilder Map(IEndpointRouteBuilder routes, string template, RequestDelegate action) =>
                routes.Map(template, action);

            return new object[][]
            {
                    new object[] { (Func<IEndpointRouteBuilder, string, RequestDelegate, IEndpointConventionBuilder>)MapGet },
                    new object[] { (Func<IEndpointRouteBuilder, string, RequestDelegate, IEndpointConventionBuilder>)MapPost },
                    new object[] { (Func<IEndpointRouteBuilder, string, RequestDelegate, IEndpointConventionBuilder>)MapPut },
                    new object[] { (Func<IEndpointRouteBuilder, string, RequestDelegate, IEndpointConventionBuilder>)MapDelete },
                    new object[] { (Func<IEndpointRouteBuilder, string, RequestDelegate, IEndpointConventionBuilder>)Map },
            };
        }
    }

    [Fact]
    public void MapEndpoint_StringPattern_BuildsEndpoint()
    {
        // Arrange
        var builder = new DefaultEndpointRouteBuilder(new ApplicationBuilder(EmptyServiceProvider.Instance));
        RequestDelegate requestDelegate = (d) => Task.CompletedTask;

        // Act
        var endpointBuilder = builder.Map("/", requestDelegate);

        // Assert
        var endpointBuilder1 = GetRouteEndpointBuilder(builder);

        Assert.Equal(requestDelegate, endpointBuilder1.RequestDelegate);
        Assert.Equal("/", endpointBuilder1.DisplayName);
        Assert.Equal("/", endpointBuilder1.RoutePattern.RawText);
    }

    [Theory]
    [MemberData(nameof(MapMethods))]
    public async Task MapEndpoint_ReturnGenericTypeTask_GeneratedDelegate(Func<IEndpointRouteBuilder, string, RequestDelegate, IEndpointConventionBuilder> map)
    {
        var httpContext = new DefaultHttpContext();
        var responseBodyStream = new MemoryStream();
        httpContext.Response.Body = responseBodyStream;

        // Arrange
        var builder = new DefaultEndpointRouteBuilder(new ApplicationBuilder(EmptyServiceProvider.Instance));
        static async Task<string> GenericTypeTaskDelegate(HttpContext context) => await Task.FromResult("String Test");

        // Act
        var endpointBuilder = map(builder, "/", GenericTypeTaskDelegate);

        // Assert
        var dataSource = GetBuilderEndpointDataSource(builder);
        var endpoint = Assert.Single(dataSource.Endpoints); // Triggers build and construction of delegate

        Assert.NotNull(endpoint.RequestDelegate);
        var requestDelegate = endpoint.RequestDelegate!;
        await requestDelegate(httpContext);

        var responseBody = Encoding.UTF8.GetString(responseBodyStream.ToArray());

        Assert.Equal("String Test", responseBody);
    }

    [Theory]
    [MemberData(nameof(MapMethods))]
    public async Task MapEndpoint_CanBeFiltered_ByRouteHandlerFilters(Func<IEndpointRouteBuilder, string, RequestDelegate, IEndpointConventionBuilder> map)
    {
        var builder = new DefaultEndpointRouteBuilder(new ApplicationBuilder(EmptyServiceProvider.Instance));
        var httpContext = new DefaultHttpContext();
        var responseBodyStream = new MemoryStream();
        httpContext.Response.Body = responseBodyStream;

        RequestDelegate initialRequestDelegate = static (context) => Task.CompletedTask;
        var filterTag = new TagsAttribute("filter");

        var endpointBuilder = map(builder, "/", initialRequestDelegate).AddRouteHandlerFilter(filterFactory: (routeHandlerContext, next) =>
        {
            routeHandlerContext.EndpointMetadata.Add(filterTag);
            return async invocationContext =>
            {
                Assert.IsAssignableFrom<HttpContext>(Assert.Single(invocationContext.Arguments));
                // Ignore thre result and write filtered because we can!
                await next(invocationContext);
                return "filtered!";
            };
        });

        var dataSource = GetBuilderEndpointDataSource(builder);
        var endpoint = Assert.Single(dataSource.Endpoints);

        Assert.NotSame(initialRequestDelegate, endpoint.RequestDelegate);
        Assert.Same(filterTag, endpoint.Metadata.GetMetadata<ITagsMetadata>());

        Assert.NotNull(endpoint.RequestDelegate);
        var requestDelegate = endpoint.RequestDelegate!;
        await requestDelegate(httpContext);

        var responseBody = Encoding.UTF8.GetString(responseBodyStream.ToArray());

        Assert.Equal("filtered!", responseBody);
    }

    [Theory]
    [MemberData(nameof(MapMethods))]
    public void MapEndpoint_UsesOriginalRequestDelegateInstance_IfFilterDoesNotChangePerRequestBehavior(Func<IEndpointRouteBuilder, string, RequestDelegate, IEndpointConventionBuilder> map)
    {
        var builder = new DefaultEndpointRouteBuilder(new ApplicationBuilder(EmptyServiceProvider.Instance));

        RequestDelegate initialRequestDelegate = static (context) => Task.CompletedTask;
        var filterTag = new TagsAttribute("filter");

        var endpointBuilder = map(builder, "/", initialRequestDelegate).AddRouteHandlerFilter((routeHandlerContext, next) =>
        {
            routeHandlerContext.EndpointMetadata.Add(filterTag);
            return next;
        });

        var dataSource = GetBuilderEndpointDataSource(builder);
        var endpoint = Assert.Single(dataSource.Endpoints);

        Assert.Same(initialRequestDelegate, endpoint.RequestDelegate);
        Assert.Same(filterTag, endpoint.Metadata.GetMetadata<ITagsMetadata>());
    }

    [Fact]
    public void MapEndpoint_TypedPattern_BuildsEndpoint()
    {
        // Arrange
        var builder = new DefaultEndpointRouteBuilder(new ApplicationBuilder(EmptyServiceProvider.Instance));
        RequestDelegate requestDelegate = (d) => Task.CompletedTask;

        // Act
        var endpointBuilder = builder.Map(RoutePatternFactory.Parse("/"), requestDelegate);

        // Assert
        var endpointBuilder1 = GetRouteEndpointBuilder(builder);

        Assert.Equal(requestDelegate, endpointBuilder1.RequestDelegate);
        Assert.Equal("/", endpointBuilder1.DisplayName);
        Assert.Equal("/", endpointBuilder1.RoutePattern.RawText);
    }

    [Fact]
    public void MapEndpoint_AttributesCollectedAsMetadata()
    {
        // Arrange
        var builder = new DefaultEndpointRouteBuilder(new ApplicationBuilder(EmptyServiceProvider.Instance));

        // Act
        var endpointBuilder = builder.Map(RoutePatternFactory.Parse("/"), Handle);

        // Assert
        var endpointBuilder1 = GetRouteEndpointBuilder(builder);
        Assert.Equal("/", endpointBuilder1.RoutePattern.RawText);
        Assert.Equal(2, endpointBuilder1.Metadata.Count);
        Assert.IsType<Attribute1>(endpointBuilder1.Metadata[0]);
        Assert.IsType<Attribute2>(endpointBuilder1.Metadata[1]);
    }

    [Fact]
    public void MapEndpoint_GeneratedDelegateWorks()
    {
        // Arrange
        var builder = new DefaultEndpointRouteBuilder(new ApplicationBuilder(EmptyServiceProvider.Instance));

        Expression<RequestDelegate> handler = context => Task.CompletedTask;

        // Act
        var endpointBuilder = builder.Map(RoutePatternFactory.Parse("/"), handler.Compile());

        // Assert
        var endpointBuilder1 = GetRouteEndpointBuilder(builder);
        Assert.Equal("/", endpointBuilder1.RoutePattern.RawText);
    }

    [Fact]
    public void MapEndpoint_PrecedenceOfMetadata_BuilderMetadataReturned()
    {
        // Arrange
        var builder = new DefaultEndpointRouteBuilder(new ApplicationBuilder(EmptyServiceProvider.Instance));

        // Act
        var endpointBuilder = builder.MapMethods("/", new[] { "METHOD" }, HandleHttpMetdata);
        endpointBuilder.WithMetadata(new HttpMethodMetadata(new[] { "BUILDER" }));

        // Assert
        var dataSource = Assert.Single(builder.DataSources);
        var endpoint = Assert.Single(dataSource.Endpoints);

        Assert.Equal(3, endpoint.Metadata.Count);
        Assert.Equal("ATTRIBUTE", GetMethod(endpoint.Metadata[0]));
        Assert.Equal("METHOD", GetMethod(endpoint.Metadata[1]));
        Assert.Equal("BUILDER", GetMethod(endpoint.Metadata[2]));

        Assert.Equal("BUILDER", endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Single());

        string GetMethod(object metadata)
        {
            var httpMethodMetadata = Assert.IsAssignableFrom<IHttpMethodMetadata>(metadata);
            return Assert.Single(httpMethodMetadata.HttpMethods);
        }
    }

    [Theory]
    [MemberData(nameof(MapMethods))]
    public void Map_EndpointMetadataNotDuplicated(Func<IEndpointRouteBuilder, string, RequestDelegate, IEndpointConventionBuilder> map)
    {
        // Arrange
        var builder = new DefaultEndpointRouteBuilder(new ApplicationBuilder(EmptyServiceProvider.Instance));

        // Act
        var endpointBuilder = map(builder, "/", context => Task.CompletedTask).WithMetadata(new EndpointNameMetadata("MapMe"));

        // Assert
        var ds = GetBuilderEndpointDataSource(builder);

        _ = ds.Endpoints;
        _ = ds.Endpoints;
        _ = ds.Endpoints;

        Assert.Single(ds.Endpoints);
        var endpoint = ds.Endpoints.Single();

        Assert.Single(endpoint.Metadata.GetOrderedMetadata<IEndpointNameMetadata>());
    }

    [Theory]
    [MemberData(nameof(MapMethods))]
    public void AddingMetadataAfterBuildingEndpointThrows(Func<IEndpointRouteBuilder, string, RequestDelegate, IEndpointConventionBuilder> map)
    {
        // Arrange
        var builder = new DefaultEndpointRouteBuilder(new ApplicationBuilder(EmptyServiceProvider.Instance));

        // Act
        var endpointBuilder = map(builder, "/", context => Task.CompletedTask).WithMetadata(new EndpointNameMetadata("MapMe"));

        // Assert
        var ds = GetBuilderEndpointDataSource(builder);

        var endpoint = Assert.Single(ds.Endpoints);

        Assert.Single(endpoint.Metadata.GetOrderedMetadata<IEndpointNameMetadata>());

        Assert.Throws<InvalidOperationException>(() => endpointBuilder.WithMetadata(new RouteNameMetadata("Foo")));
    }

    [Fact]
    public void Map_AddsMetadata_InCorrectOrder()
    {
        // Arrange
        var builder = new DefaultEndpointRouteBuilder(new ApplicationBuilder(EmptyServiceProvider.Instance));
        var @delegate = [Attribute1, Attribute2] (AddsCustomParameterMetadata param1) => new AddsCustomEndpointMetadataResult();

        // Act
        builder.Map("/test", @delegate);

        // Assert
        var ds = GetBuilderEndpointDataSource(builder);
        var endpoint = Assert.Single(ds.Endpoints);
        var metadata = endpoint.Metadata;

        Assert.Collection(metadata,
            m => Assert.IsAssignableFrom<MethodInfo>(m),
            m => Assert.Equal("System.Runtime.CompilerServices.NullableContextAttribute", m.ToString()),
            m => Assert.IsAssignableFrom<Attribute1>(m),
            m => Assert.IsAssignableFrom<Attribute2>(m),
            m => Assert.IsAssignableFrom<ParameterNameMetadata>(m),
            m =>
            {
                Assert.IsAssignableFrom<CustomEndpointMetadata>(m);
                Assert.Equal(MetadataSource.Parameter, ((CustomEndpointMetadata)m).Source);
            },
            m =>
            {
                Assert.IsAssignableFrom<CustomEndpointMetadata>(m);
                Assert.Equal(MetadataSource.ReturnType, ((CustomEndpointMetadata)m).Source);
            });
    }

    [Attribute1]
    [Attribute2]
    private static Task Handle(HttpContext context) => Task.CompletedTask;

    [HttpMethod("ATTRIBUTE")]
    private static Task HandleHttpMetdata(HttpContext context) => Task.CompletedTask;

    private class HttpMethodAttribute : Attribute, IHttpMethodMetadata
    {
        public bool AcceptCorsPreflight => false;

        public IReadOnlyList<string> HttpMethods { get; }

        public HttpMethodAttribute(params string[] httpMethods)
        {
            HttpMethods = httpMethods;
        }
    }

    private class Attribute1 : Attribute
    {
    }

    private class Attribute2 : Attribute
    {
    }

    private class AddsCustomEndpointMetadataResult : IEndpointMetadataProvider, IResult
    {
        public static void PopulateMetadata(EndpointMetadataContext context)
        {
            context.EndpointMetadata.Add(new CustomEndpointMetadata { Source = MetadataSource.ReturnType });
        }

        public Task ExecuteAsync(HttpContext httpContext) => throw new NotImplementedException();
    }

    private class AddsCustomParameterMetadata : IEndpointParameterMetadataProvider, IEndpointMetadataProvider
    {
        public static ValueTask<AddsCustomParameterMetadata> BindAsync(HttpContext context, ParameterInfo parameter) => default;

        public static void PopulateMetadata(EndpointParameterMetadataContext parameterContext)
        {
            parameterContext.EndpointMetadata.Add(new ParameterNameMetadata { Name = parameterContext.Parameter.Name ?? string.Empty });
        }

        public static void PopulateMetadata(EndpointMetadataContext context)
        {
            context.EndpointMetadata.Add(new CustomEndpointMetadata { Source = MetadataSource.Parameter });
        }
    }

    private class ParameterNameMetadata
    {
        public string Name { get; init; } = string.Empty;
    }

    private class CustomEndpointMetadata
    {
        public string Data { get; init; } = string.Empty;

        public MetadataSource Source { get; init; }
    }

    private enum MetadataSource
    {
        Parameter,
        ReturnType
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static EmptyServiceProvider Instance { get; } = new EmptyServiceProvider();
        public object? GetService(Type serviceType) => null;
    }
}
