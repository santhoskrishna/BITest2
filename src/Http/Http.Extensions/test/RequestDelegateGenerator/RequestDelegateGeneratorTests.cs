// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Generators.StaticRouteHandlerModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Http.Generators.Tests;

public class RequestDelegateGeneratorTests : RequestDelegateGeneratorTestBase
{
    [Theory]
    [InlineData(@"app.MapGet(""/hello"", () => ""Hello world!"");", "MapGet", "Hello world!")]
    [InlineData(@"app.MapPost(""/hello"", () => ""Hello world!"");", "MapPost", "Hello world!")]
    [InlineData(@"app.MapDelete(""/hello"", () => ""Hello world!"");", "MapDelete", "Hello world!")]
    [InlineData(@"app.MapPut(""/hello"", () => ""Hello world!"");", "MapPut", "Hello world!")]
    [InlineData(@"app.MapGet(pattern: ""/hello"", handler: () => ""Hello world!"");", "MapGet", "Hello world!")]
    [InlineData(@"app.MapPost(handler: () => ""Hello world!"", pattern: ""/hello"");", "MapPost", "Hello world!")]
    [InlineData(@"app.MapDelete(pattern: ""/hello"", handler: () => ""Hello world!"");", "MapDelete", "Hello world!")]
    [InlineData(@"app.MapPut(handler: () => ""Hello world!"", pattern: ""/hello"");", "MapPut", "Hello world!")]
    public async Task MapAction_NoParam_StringReturn(string source, string httpMethod, string expectedBody)
    {
        var (result, compilation) = await RunGeneratorAsync(source);

        var endpointModel = GetStaticEndpoint(result, GeneratorSteps.EndpointModelStep);
        var endpoint = GetEndpointFromCompilation(compilation);

        Assert.Equal("/hello", endpointModel.RoutePattern);
        Assert.Equal(httpMethod, endpointModel.HttpMethod);

        var httpContext = CreateHttpContext();
        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody);
    }

    public static object[][] MapAction_ExplicitQueryParam_StringReturn_Data
    {
        get
        {
            var expectedBody = "TestQueryValue";
            var fromQueryRequiredSource = """app.MapGet("/", ([FromQuery] string queryValue) => queryValue);""";
            var fromQueryWithNameRequiredSource = """app.MapGet("/", ([FromQuery(Name = "queryValue")] string parameterName) => parameterName);""";
            var fromQueryWithNullNameRequiredSource = """app.MapGet("/", ([FromQuery(Name = null)] string queryValue) => queryValue);""";
            var fromQueryNullableSource = """app.MapGet("/", ([FromQuery] string? queryValue) => queryValue ?? string.Empty);""";
            var fromQueryDefaultValueSource = """
#nullable disable
string getQueryWithDefault([FromQuery] string queryValue = null) => queryValue ?? string.Empty;
app.MapGet("/", getQueryWithDefault);
#nullable restore
""";

            return new[]
            {
                new object[] { fromQueryRequiredSource, expectedBody, 200, expectedBody },
                new object[] { fromQueryRequiredSource, null, 400, string.Empty },
                new object[] { fromQueryWithNameRequiredSource, expectedBody, 200, expectedBody },
                new object[] { fromQueryWithNameRequiredSource, null, 400, string.Empty },
                new object[] { fromQueryWithNullNameRequiredSource, expectedBody, 200, expectedBody },
                new object[] { fromQueryWithNullNameRequiredSource, null, 400, string.Empty },
                new object[] { fromQueryNullableSource, expectedBody, 200, expectedBody },
                new object[] { fromQueryNullableSource, null, 200, string.Empty },
                new object[] { fromQueryDefaultValueSource, expectedBody, 200, expectedBody },
                new object[] { fromQueryDefaultValueSource, null, 200, string.Empty },
            };
        }
    }

    [Theory]
    [MemberData(nameof(MapAction_ExplicitQueryParam_StringReturn_Data))]
    public async Task MapAction_ExplicitQueryParam_StringReturn(string source, string queryValue, int expectedStatusCode, string expectedBody)
    {
        var (results, compilation) = await RunGeneratorAsync(source);

        var endpointModel = GetStaticEndpoint(results, GeneratorSteps.EndpointModelStep);
        var endpoint = GetEndpointFromCompilation(compilation);

        Assert.Equal("/", endpointModel.RoutePattern);
        Assert.Equal("MapGet", endpointModel.HttpMethod);
        var p = Assert.Single(endpointModel.Parameters);
        Assert.Equal(EndpointParameterSource.Query, p.Source);
        Assert.Equal("queryValue", p.Name);

        var httpContext = CreateHttpContext();
        if (queryValue is not null)
        {
            httpContext.Request.QueryString = new QueryString($"?queryValue={queryValue}");
        }

        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody, expectedStatusCode);
    }

    [Fact]
    public async Task MapAction_SingleDateOnlyParam_StringReturn()
    {
        var (results, compilation) = await RunGeneratorAsync("""
app.MapGet("/hello", ([FromQuery]DateOnly p) => p.ToString("yyyy-MM-dd"));
""");
        var endpointModel = GetStaticEndpoint(results, GeneratorSteps.EndpointModelStep);
        var endpoint = GetEndpointFromCompilation(compilation);

        Assert.Equal("/hello", endpointModel.RoutePattern);
        Assert.Equal("MapGet", endpointModel.HttpMethod);
        var p = Assert.Single(endpointModel.Parameters);
        Assert.Equal(EndpointParameterSource.Query, p.Source);
        Assert.Equal("p", p.Name);

        var httpContext = CreateHttpContext();
        httpContext.Request.QueryString = new QueryString("?p=2023-02-20");

        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "2023-02-20");
        await VerifyAgainstBaselineUsingFile(compilation);
    }

    [Fact]
    public async Task MapAction_SingleTimeOnlyParam_StringReturn()
    {
        var (results, compilation) = await RunGeneratorAsync("""
app.MapGet("/hello", ([FromQuery]TimeOnly p) => p.ToString());
""");
        var endpointModel = GetStaticEndpoint(results, GeneratorSteps.EndpointModelStep);
        var endpoint = GetEndpointFromCompilation(compilation);

        Assert.Equal("/hello", endpointModel.RoutePattern);
        Assert.Equal("MapGet", endpointModel.HttpMethod);
        var p = Assert.Single(endpointModel.Parameters);
        Assert.Equal(EndpointParameterSource.Query, p.Source);
        Assert.Equal("p", p.Name);

        var httpContext = CreateHttpContext();
        httpContext.Request.QueryString = new QueryString("?p=13:30");

        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "1:30 PM");
        await VerifyAgainstBaselineUsingFile(compilation);
    }

    [Fact]
    public async Task MapAction_SingleDateTimeParam_StringReturn()
    {
        var (results, compilation) = await RunGeneratorAsync("""
app.MapGet("/hello", ([FromQuery]DateTime p) => p.ToString("yyyy-MM-dd"));
""");
        var endpointModel = GetStaticEndpoint(results, GeneratorSteps.EndpointModelStep);
        var endpoint = GetEndpointFromCompilation(compilation);

        Assert.Equal("/hello", endpointModel.RoutePattern);
        Assert.Equal("MapGet", endpointModel.HttpMethod);
        var p = Assert.Single(endpointModel.Parameters);
        Assert.Equal(EndpointParameterSource.Query, p.Source);
        Assert.Equal("p", p.Name);

        var httpContext = CreateHttpContext();
        httpContext.Request.QueryString = new QueryString("?p=2023-02-20");

        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "2023-02-20");
        await VerifyAgainstBaselineUsingFile(compilation);
    }

    [Fact]
    public async Task MapAction_SingleDateTimeOffsetParam_StringReturn()
    {
        var (results, compilation) = await RunGeneratorAsync("""
app.MapGet("/hello", ([FromQuery]DateTimeOffset p) => p.ToString("yyyy-MM-dd"));
""");
        var endpointModel = GetStaticEndpoint(results, GeneratorSteps.EndpointModelStep);
        var endpoint = GetEndpointFromCompilation(compilation);

        Assert.Equal("/hello", endpointModel.RoutePattern);
        Assert.Equal("MapGet", endpointModel.HttpMethod);
        var p = Assert.Single(endpointModel.Parameters);
        Assert.Equal(EndpointParameterSource.Query, p.Source);
        Assert.Equal("p", p.Name);

        var httpContext = CreateHttpContext();
        httpContext.Request.QueryString = new QueryString("?p=2023-02-20");

        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "2023-02-20");
        await VerifyAgainstBaselineUsingFile(compilation);
    }

    [Theory]
    [InlineData("sbyte")]
    [InlineData("SByte")]
    [InlineData("byte")]
    [InlineData("Byte")]
    [InlineData("short")]
    [InlineData("Int16")]
    [InlineData("ushort")]
    [InlineData("UInt16")]
    [InlineData("int")]
    [InlineData("Int32")]
    [InlineData("uint")]
    [InlineData("UInt32")]
    [InlineData("long")]
    [InlineData("Int64")]
    [InlineData("ulong")]
    [InlineData("UInt64")]
    [InlineData("float")]
    [InlineData("Single")]
    [InlineData("double")]
    [InlineData("Double")]
    [InlineData("decimal")]
    [InlineData("Decimal")]
    public async Task MapAction_SingleNumericParam_StringReturn(string numericType)
    {
        var (results, compilation) = await RunGeneratorAsync($$"""
app.MapGet("/hello", ([FromQuery]{{numericType}} p) => p.ToString());
""");

        var endpointModel = GetStaticEndpoint(results, GeneratorSteps.EndpointModelStep);
        var endpoint = GetEndpointFromCompilation(compilation);

        Assert.Equal("/hello", endpointModel.RoutePattern);
        Assert.Equal("MapGet", endpointModel.HttpMethod);
        var p = Assert.Single(endpointModel.Parameters);
        Assert.Equal(EndpointParameterSource.Query, p.Source);
        Assert.Equal("p", p.Name);

        var httpContext = CreateHttpContext();
        httpContext.Request.QueryString = new QueryString("?p=42");

        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "42");
        await VerifyAgainstBaselineUsingFile(compilation);
    }

    [Fact]
    public async Task MapAction_SingleComplexTypeParam_StringReturn()
    {
        // HACK! Notice the return value of p.Name! - this is because TestMapActions.cs has #nullable enable
        // set and the compiler is returning when it is simply p.Name:
        //
        //     CS8603: Possible null reference return.
        //
        // Without source gen this same code isn't a problem.
        var (results, compilation) = await RunGeneratorAsync("""
app.MapGet("/hello", ([FromQuery]Todo p) => p.Name!);
""");

        var endpointModel = GetStaticEndpoint(results, GeneratorSteps.EndpointModelStep);
        var endpoint = GetEndpointFromCompilation(compilation);

        Assert.Equal("/hello", endpointModel.RoutePattern);
        Assert.Equal("MapGet", endpointModel.HttpMethod);
        var p = Assert.Single(endpointModel.Parameters);
        Assert.Equal(EndpointParameterSource.Query, p.Source);
        Assert.Equal("p", p.Name);

        var httpContext = CreateHttpContext();
        httpContext.Request.QueryString = new QueryString("?p=1");

        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "Knit kitten mittens.");
        await VerifyAgainstBaselineUsingFile(compilation);
    }

    [Fact]
    public async Task MapAction_SingleEnumParam_StringReturn()
    {
        // HACK! Notice the return value of p.Name! - this is because TestMapActions.cs has #nullable enable
        // set and the compiler is returning when it is simply p.Name:
        //
        //     CS8603: Possible null reference return.
        //
        // Without source gen this same code isn't a problem.
        var (results, compilation) = await RunGeneratorAsync("""
app.MapGet("/hello", ([FromQuery]TodoStatus p) => p.ToString());
""");

        var endpointModel = GetStaticEndpoint(results, GeneratorSteps.EndpointModelStep);
        var endpoint = GetEndpointFromCompilation(compilation);

        Assert.Equal("/hello", endpointModel.RoutePattern);
        Assert.Equal("MapGet", endpointModel.HttpMethod);
        var p = Assert.Single(endpointModel.Parameters);
        Assert.Equal(EndpointParameterSource.Query, p.Source);
        Assert.Equal("p", p.Name);

        var httpContext = CreateHttpContext();
        httpContext.Request.QueryString = new QueryString("?p=Done");

        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "Done");
        await VerifyAgainstBaselineUsingFile(compilation);
    }

    // [Fact]
    // public async Task MapAction_SingleNullableStringParam_WithQueryStringValueProvided_StringReturn()

    [Fact]
    public async Task MapAction_SingleNullableStringParam_WithEmptyQueryStringValueProvided_StringReturn()
    {
        var (results, compilation) = await RunGeneratorAsync("""
app.MapGet("/hello", ([FromQuery]string? p) => p == string.Empty ? "No value, but not null!" : "Was null!");
""");

        var endpointModel = GetStaticEndpoint(results, GeneratorSteps.EndpointModelStep);
        var endpoint = GetEndpointFromCompilation(compilation);

        Assert.Equal("/hello", endpointModel.RoutePattern);
        Assert.Equal("MapGet", endpointModel.HttpMethod);
        var p = Assert.Single(endpointModel.Parameters);
        Assert.Equal(EndpointParameterSource.Query, p.Source);
        Assert.Equal("p", p.Name);

        var httpContext = CreateHttpContext();
        httpContext.Request.QueryString = new QueryString("?p=");

        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "No value, but not null!");
        await VerifyAgainstBaselineUsingFile(compilation);
    }

    [Fact]
    public async Task MapAction_MultipleStringParam_StringReturn()
    {
        var (results, compilation) = await RunGeneratorAsync("""
app.MapGet("/hello", ([FromQuery]string p1, [FromQuery]string p2) => $"{p1} {p2}");
""");

        var endpointModel = GetStaticEndpoint(results, GeneratorSteps.EndpointModelStep);
        var endpoint = GetEndpointFromCompilation(compilation);

        Assert.Equal("/hello", endpointModel.RoutePattern);
        Assert.Equal("MapGet", endpointModel.HttpMethod);

        var httpContext = CreateHttpContext();
        httpContext.Request.QueryString = new QueryString("?p1=Hello&p2=world!");

        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "Hello world!");
        await VerifyAgainstBaselineUsingFile(compilation);
    }

    [Theory]
    [InlineData("HttpContext")]
    [InlineData("HttpRequest")]
    [InlineData("HttpResponse")]
    [InlineData("System.IO.Pipelines.PipeReader")]
    [InlineData("System.IO.Stream")]
    [InlineData("System.Security.Claims.ClaimsPrincipal")]
    [InlineData("System.Threading.CancellationToken")]
    public async Task MapAction_SingleSpecialTypeParam_StringReturn(string parameterType)
    {
        var (results, compilation) = await RunGeneratorAsync($"""
app.MapGet("/hello", ({parameterType} p) => p == null ? "null!" : "Hello world!");
""");

        var endpointModel = GetStaticEndpoint(results, GeneratorSteps.EndpointModelStep);
        var endpoint = GetEndpointFromCompilation(compilation);

        Assert.Equal("/hello", endpointModel.RoutePattern);
        Assert.Equal("MapGet", endpointModel.HttpMethod);
        var p = Assert.Single(endpointModel.Parameters);
        Assert.Equal(EndpointParameterSource.SpecialType, p.Source);
        Assert.Equal("p", p.Name);

        var httpContext = CreateHttpContext();
        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "Hello world!");
    }

    [Fact]
    public async Task MapAction_MultipleSpecialTypeParam_StringReturn()
    {
        var (results, compilation) = await RunGeneratorAsync("""
app.MapGet("/hello", (HttpRequest req, HttpResponse res) => req is null || res is null ? "null!" : "Hello world!");
""");

        var endpointModel = GetStaticEndpoint(results, GeneratorSteps.EndpointModelStep);
        var endpoint = GetEndpointFromCompilation(compilation);

        Assert.Equal("/hello", endpointModel.RoutePattern);
        Assert.Equal("MapGet", endpointModel.HttpMethod);

        Assert.Collection(endpointModel.Parameters,
            reqParam =>
            {
                Assert.Equal(EndpointParameterSource.SpecialType, reqParam.Source);
                Assert.Equal("req", reqParam.Name);
            },
            reqParam =>
            {
                Assert.Equal(EndpointParameterSource.SpecialType, reqParam.Source);
                Assert.Equal("res", reqParam.Name);
            });

        var httpContext = CreateHttpContext();
        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "Hello world!");
        await VerifyAgainstBaselineUsingFile(compilation);
    }

    [Fact]
    public async Task MapGet_WithRequestDelegate_DoesNotGenerateSources()
    {
        var (results, compilation) = await RunGeneratorAsync("""
app.MapGet("/hello", (HttpContext context) => Task.CompletedTask);
""");

        Assert.Empty(GetStaticEndpoints(results, GeneratorSteps.EndpointModelStep));

        var endpoint = GetEndpointFromCompilation(compilation, expectSourceKey: false);

        var httpContext = CreateHttpContext();
        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "");
    }

    [Fact]
    public async Task MapAction_MultilineLambda()
    {
        var source = """
app.MapGet("/hello", () =>
{
    return "Hello world!";
});
""";
        var (result, compilation) = await RunGeneratorAsync(source);

        var endpointModel = GetStaticEndpoint(result, GeneratorSteps.EndpointModelStep);
        var endpoint = GetEndpointFromCompilation(compilation);

        Assert.Equal("/hello", endpointModel.RoutePattern);

        var httpContext = CreateHttpContext();
        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "Hello world!");
    }

    [Fact]
    public async Task MapAction_NoParam_StringReturn_WithFilter()
    {
        var source = """
app.MapGet("/hello", () => "Hello world!")
    .AddEndpointFilter(async (context, next) => {
        var result = await next(context);
        return $"Filtered: {result}";
    });
""";
        var expectedBody = "Filtered: Hello world!";
        var (result, compilation) = await RunGeneratorAsync(source);

        await VerifyAgainstBaselineUsingFile(compilation);

        var endpointModel = GetStaticEndpoint(result, GeneratorSteps.EndpointModelStep);
        var endpoint = GetEndpointFromCompilation(compilation);

        Assert.Equal("/hello", endpointModel.RoutePattern);

        var httpContext = CreateHttpContext();
        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody);
    }

    [Theory]
    [InlineData(@"app.MapGet(""/"", () => 123456);", "123456")]
    [InlineData(@"app.MapGet(""/"", () => true);", "true")]
    [InlineData(@"app.MapGet(""/"", () => new DateTime(2023, 1, 1));", @"""2023-01-01T00:00:00""")]
    public async Task MapAction_NoParam_AnyReturn(string source, string expectedBody)
    {
        var (result, compilation) = await RunGeneratorAsync(source);

        var endpointModel = GetStaticEndpoint(result, GeneratorSteps.EndpointModelStep);
        var endpoint = GetEndpointFromCompilation(compilation);

        Assert.Equal("/", endpointModel.RoutePattern);
        Assert.Equal("MapGet", endpointModel.HttpMethod);

        var httpContext = CreateHttpContext();
        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody);
    }

    public static IEnumerable<object[]> MapAction_NoParam_ComplexReturn_Data => new List<object[]>()
    {
        new object[] { """app.MapGet("/", () => new Todo() { Name = "Test Item"});""" },
        new object[] { """
object GetTodo() => new Todo() { Name = "Test Item"};
app.MapGet("/", GetTodo);
"""},
        new object[] { """app.MapGet("/", () => TypedResults.Ok(new Todo() { Name = "Test Item"}));""" }
    };

    [Theory]
    [MemberData(nameof(MapAction_NoParam_ComplexReturn_Data))]
    public async Task MapAction_NoParam_ComplexReturn(string source)
    {
        var expectedBody = """{"id":0,"name":"Test Item","isComplete":false}""";
        var (result, compilation) = await RunGeneratorAsync(source);

        var endpointModel = GetStaticEndpoint(result, GeneratorSteps.EndpointModelStep);
        var endpoint = GetEndpointFromCompilation(compilation);

        Assert.Equal("/", endpointModel.RoutePattern);
        Assert.Equal("MapGet", endpointModel.HttpMethod);

        var httpContext = CreateHttpContext();
        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody);
    }

    public static IEnumerable<object[]> MapAction_ProducesCorrectContentType_Data => new List<object[]>()
    {
        new object[] { @"app.MapGet(""/"", () => Console.WriteLine(""Returns void""));", null },
        new object[] { @"app.MapGet(""/"", () => TypedResults.Ok(""Alright!""));", null },
        new object[] { @"app.MapGet(""/"", () => Results.NotFound(""Oops!""));", null },
        new object[] { @"app.MapGet(""/"", () => Task.FromResult(new Todo() { Name = ""Test Item"" }));", "application/json" },
        new object[] { @"app.MapGet(""/"", () => ""Hello world!"");", "text/plain" }
    };

    [Theory]
    [MemberData(nameof(MapAction_ProducesCorrectContentType_Data))]
    public async Task MapAction_ProducesCorrectContentType(string source, string expectedContentType)
    {
        var (result, compilation) = await RunGeneratorAsync(source);

        var endpointModel = GetStaticEndpoint(result, GeneratorSteps.EndpointModelStep);

        Assert.Equal("/", endpointModel.RoutePattern);
        Assert.Equal("MapGet", endpointModel.HttpMethod);
        Assert.Equal(expectedContentType, endpointModel.Response.ContentType);
    }

    public static IEnumerable<object[]>  MapAction_NoParam_TaskOfTReturn_Data => new List<object[]>()
    {
        new object[] { @"app.MapGet(""/"", () => Task.FromResult(""Hello world!""));", "Hello world!" },
        new object[] { @"app.MapGet(""/"", () => Task.FromResult(new Todo() { Name = ""Test Item"" }));", """{"id":0,"name":"Test Item","isComplete":false}""" },
        new object[] { @"app.MapGet(""/"", () => Task.FromResult(TypedResults.Ok(new Todo() { Name = ""Test Item"" })));", """{"id":0,"name":"Test Item","isComplete":false}""" }
    };

    [Theory]
    [MemberData(nameof(MapAction_NoParam_TaskOfTReturn_Data))]
    public async Task MapAction_NoParam_TaskOfTReturn(string source, string expectedBody)
    {
        var (result, compilation) = await RunGeneratorAsync(source);

        var endpointModel = GetStaticEndpoint(result, GeneratorSteps.EndpointModelStep);
        var endpoint = GetEndpointFromCompilation(compilation);

        Assert.Equal("/", endpointModel.RoutePattern);
        Assert.Equal("MapGet", endpointModel.HttpMethod);
        Assert.True(endpointModel.Response.IsAwaitable);

        var httpContext = CreateHttpContext();
        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody);
    }

    public static IEnumerable<object[]> MapAction_NoParam_ValueTaskOfTReturn_Data => new List<object[]>()
    {
        new object[] { @"app.MapGet(""/"", () => ValueTask.FromResult(""Hello world!""));", "Hello world!" },
        new object[] { @"app.MapGet(""/"", () => ValueTask.FromResult(new Todo() { Name = ""Test Item""}));", """{"id":0,"name":"Test Item","isComplete":false}""" },
        new object[] { @"app.MapGet(""/"", () => ValueTask.FromResult(TypedResults.Ok(new Todo() { Name = ""Test Item""})));", """{"id":0,"name":"Test Item","isComplete":false}""" }
    };

    [Theory]
    [MemberData(nameof(MapAction_NoParam_ValueTaskOfTReturn_Data))]
    public async Task MapAction_NoParam_ValueTaskOfTReturn(string source, string expectedBody)
    {
        var (result, compilation) = await RunGeneratorAsync(source);

        var endpointModel = GetStaticEndpoint(result, GeneratorSteps.EndpointModelStep);
        var endpoint = GetEndpointFromCompilation(compilation);

        Assert.Equal("/", endpointModel.RoutePattern);
        Assert.Equal("MapGet", endpointModel.HttpMethod);
        Assert.True(endpointModel.Response.IsAwaitable);

        var httpContext = CreateHttpContext();
        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody);
    }

    public static IEnumerable<object[]> MapAction_NoParam_TaskLikeOfObjectReturn_Data => new List<object[]>()
    {
        new object[] { @"app.MapGet(""/"", () => new ValueTask<object>(""Hello world!""));", "Hello world!" },
        new object[] { @"app.MapGet(""/"", () => Task<object>.FromResult(""Hello world!""));", "Hello world!" },
        new object[] { @"app.MapGet(""/"", () => new ValueTask<object>(new Todo() { Name = ""Test Item""}));", """{"id":0,"name":"Test Item","isComplete":false}""" },
        new object[] { @"app.MapGet(""/"", () => Task<object>.FromResult(new Todo() { Name = ""Test Item""}));", """{"id":0,"name":"Test Item","isComplete":false}""" },
        new object[] { @"app.MapGet(""/"", () => new ValueTask<object>(TypedResults.Ok(new Todo() { Name = ""Test Item""})));", """{"id":0,"name":"Test Item","isComplete":false}""" },
        new object[] { @"app.MapGet(""/"", () => Task<object>.FromResult(TypedResults.Ok(new Todo() { Name = ""Test Item""})));", """{"id":0,"name":"Test Item","isComplete":false}""" }
    };

    [Theory]
    [MemberData(nameof(MapAction_NoParam_TaskLikeOfObjectReturn_Data))]
    public async Task MapAction_NoParam_TaskLikeOfObjectReturn(string source, string expectedBody)
    {
        var (result, compilation) = await RunGeneratorAsync(source);

        var endpointModel = GetStaticEndpoint(result, GeneratorSteps.EndpointModelStep);
        var endpoint = GetEndpointFromCompilation(compilation);

        Assert.Equal("/", endpointModel.RoutePattern);
        Assert.Equal("MapGet", endpointModel.HttpMethod);
        Assert.True(endpointModel.Response.IsAwaitable);

        var httpContext = CreateHttpContext();
        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody);
    }

    [Fact]
    public async Task Multiple_MapAction_NoParam_StringReturn()
    {
        var source = """
app.MapGet("/en", () => "Hello world!");
app.MapGet("/es", () => "Hola mundo!");
app.MapGet("/en-task", () => Task.FromResult("Hello world!"));
app.MapGet("/es-task", () => new ValueTask<string>("Hola mundo!"));
""";
        var (_, compilation) = await RunGeneratorAsync(source);

        await VerifyAgainstBaselineUsingFile(compilation);
    }

    [Fact]
    public async Task Multiple_MapAction_WithParams_StringReturn()
    {
        var source = """
app.MapGet("/en", (HttpRequest req) => "Hello world!");
app.MapGet("/es", (HttpResponse res) => "Hola mundo!");
app.MapGet("/zh", (HttpRequest req, HttpResponse res) => "你好世界！");
""";
        var (results, compilation) = await RunGeneratorAsync(source);

        await VerifyAgainstBaselineUsingFile(compilation);

        var endpointModels = GetStaticEndpoints(results, GeneratorSteps.EndpointModelStep);

        Assert.Collection(endpointModels,
            endpointModel =>
            {
                Assert.Equal("/en", endpointModel.RoutePattern);
                Assert.Equal("MapGet", endpointModel.HttpMethod);
                var reqParam = Assert.Single(endpointModel.Parameters);
                Assert.Equal(EndpointParameterSource.SpecialType, reqParam.Source);
                Assert.Equal("req", reqParam.Name);
            },
            endpointModel =>
            {
                Assert.Equal("/es", endpointModel.RoutePattern);
                Assert.Equal("MapGet", endpointModel.HttpMethod);
                var reqParam = Assert.Single(endpointModel.Parameters);
                Assert.Equal(EndpointParameterSource.SpecialType, reqParam.Source);
                Assert.Equal("res", reqParam.Name);
            },
            endpointModel =>
            {
                Assert.Equal("/zh", endpointModel.RoutePattern);
                Assert.Equal("MapGet", endpointModel.HttpMethod);
                Assert.Collection(endpointModel.Parameters,
                    reqParam =>
                    {
                        Assert.Equal(EndpointParameterSource.SpecialType, reqParam.Source);
                        Assert.Equal("req", reqParam.Name);
                    },
                    reqParam =>
                    {
                        Assert.Equal(EndpointParameterSource.SpecialType, reqParam.Source);
                        Assert.Equal("res", reqParam.Name);
                    });
            });

        var endpoints = GetEndpointsFromCompilation(compilation);

        Assert.Equal(3, endpoints.Length);
        var httpContext = CreateHttpContext();
        await endpoints[0].RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "Hello world!");

        httpContext = CreateHttpContext();
        await endpoints[1].RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "Hola mundo!");

        httpContext = CreateHttpContext();
        await endpoints[2].RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, "你好世界！");
    }

    [Fact]
    public async Task MapAction_VariableRoutePattern_EmitsDiagnostic_NoSource()
    {
        var expectedBody = "Hello world!";
        var source = """
var route = "/en";
app.MapGet(route, () => "Hello world!");
""";
        var (result, compilation) = await RunGeneratorAsync(source);

        // Emits diagnostic but generates no source
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticDescriptors.UnableToResolveRoutePattern.Id,diagnostic.Id);
        Assert.Empty(result.GeneratedSources);

        // Falls back to runtime-generated endpoint
        var endpoint = GetEndpointFromCompilation(compilation, expectSourceKey: false);

        var httpContext = CreateHttpContext();
        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody);
    }

    public static object[][] MapAction_ExplicitBodyParam_ComplexReturn_Data
    {
        get
        {
            var expectedBody = """{"id":0,"name":"Test Item","isComplete":false}""";
            var todo = new Todo()
            {
                Id = 0,
                Name = "Test Item",
                IsComplete = false
            };
            var withFilter = """
.AddEndpointFilter((c, n) => n(c));
""";
            var fromBodyRequiredSource = """app.MapPost("/", ([FromBody] Todo todo) => TypedResults.Ok(todo));""";
            var fromBodyEmptyBodyBehaviorSource = """app.MapPost("/", ([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] Todo todo) => TypedResults.Ok(todo));""";
            var fromBodyAllowEmptySource = """app.MapPost("/", ([CustomFromBody(AllowEmpty = true)] Todo todo) => TypedResults.Ok(todo));""";
            var fromBodyNullableSource = """app.MapPost("/", ([FromBody] Todo? todo) => TypedResults.Ok(todo));""";
            var fromBodyDefaultValueSource = """
#nullable disable
IResult postTodoWithDefault([FromBody] Todo todo = null) => TypedResults.Ok(todo);
app.MapPost("/", postTodoWithDefault);
#nullable restore
""";
            var fromBodyRequiredWithFilterSource = $"""app.MapPost("/", ([FromBody] Todo todo) => TypedResults.Ok(todo)){withFilter}""";
            var fromBodyEmptyBehaviorWithFilterSource = $"""app.MapPost("/", ([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] Todo todo) => TypedResults.Ok(todo)){withFilter}""";
            var fromBodyAllowEmptyWithFilterSource = $"""app.MapPost("/", ([CustomFromBody(AllowEmpty = true)] Todo todo) => TypedResults.Ok(todo)){withFilter}""";
            var fromBodyNullableWithFilterSource = $"""app.MapPost("/", ([FromBody] Todo?  todo) => TypedResults.Ok(todo)){withFilter}""";
            var fromBodyDefaultValueWithFilterSource = $"""
#nullable disable
IResult postTodoWithDefault([FromBody] Todo todo = null) => TypedResults.Ok(todo);
app.MapPost("/", postTodoWithDefault){withFilter}
#nullable restore
""";

            return new[]
            {
                new object[] { fromBodyRequiredSource, todo, 200, expectedBody },
                new object[] { fromBodyRequiredSource, null, 400, string.Empty },
                new object[] { fromBodyEmptyBodyBehaviorSource, todo, 200, expectedBody },
                new object[] { fromBodyEmptyBodyBehaviorSource, null, 200, string.Empty },
                new object[] { fromBodyAllowEmptySource, todo, 200, expectedBody },
                new object[] { fromBodyAllowEmptySource, null, 200, string.Empty },
                new object[] { fromBodyNullableSource, todo, 200, expectedBody },
                new object[] { fromBodyNullableSource, null, 200, string.Empty },
                new object[] { fromBodyDefaultValueSource, todo, 200, expectedBody },
                new object[] { fromBodyDefaultValueSource, null, 200, string.Empty },
                new object[] { fromBodyRequiredWithFilterSource, todo, 200, expectedBody },
                new object[] { fromBodyRequiredWithFilterSource, null, 400, string.Empty },
                new object[] { fromBodyEmptyBehaviorWithFilterSource, todo, 200, expectedBody },
                new object[] { fromBodyEmptyBehaviorWithFilterSource, null, 200, string.Empty },
                new object[] { fromBodyAllowEmptyWithFilterSource, todo, 200, expectedBody },
                new object[] { fromBodyAllowEmptyWithFilterSource, null, 200, string.Empty },
                new object[] { fromBodyNullableWithFilterSource, todo, 200, expectedBody },
                new object[] { fromBodyNullableWithFilterSource, null, 200, string.Empty },
                new object[] { fromBodyDefaultValueWithFilterSource, todo, 200, expectedBody },
                new object[] { fromBodyDefaultValueSource, null, 200, string.Empty },
            };
        }
    }

    [Theory]
    [MemberData(nameof(MapAction_ExplicitBodyParam_ComplexReturn_Data))]
    public async Task MapAction_ExplicitBodyParam_ComplexReturn(string source, Todo requestData, int expectedStatusCode, string expectedBody)
    {
        var (_, compilation) = await RunGeneratorAsync(source);
        var endpoint = GetEndpointFromCompilation(compilation);

        var httpContext = CreateHttpContext();
        httpContext.Features.Set<IHttpRequestBodyDetectionFeature>(new RequestBodyDetectionFeature(true));
        httpContext.Request.Headers["Content-Type"] = "application/json";

        var requestBodyBytes = JsonSerializer.SerializeToUtf8Bytes(requestData);
        var stream = new MemoryStream(requestBodyBytes);
        httpContext.Request.Body = stream;
        httpContext.Request.Headers["Content-Length"] = stream.Length.ToString(CultureInfo.InvariantCulture);

        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody, expectedStatusCode);
    }

    [Fact]
    public async Task MapAction_ExplicitBodyParam_ComplexReturn_Snapshot()
    {
        var expectedBody = """{"id":0,"name":"Test Item","isComplete":false}""";
        var todo = new Todo()
        {
            Id = 0,
            Name = "Test Item",
            IsComplete = false
        };
        var source = $"""
app.MapPost("/fromBodyRequired", ([FromBody] Todo todo) => TypedResults.Ok(todo));
#pragma warning disable CS8622
app.MapPost("/fromBodyOptional", ([FromBody] Todo? todo) => TypedResults.Ok(todo));
#pragma warning restore CS8622
""";
        var (_, compilation) = await RunGeneratorAsync(source);

        await VerifyAgainstBaselineUsingFile(compilation);

        var endpoints = GetEndpointsFromCompilation(compilation);

        Assert.Equal(2, endpoints.Length);

        // formBodyRequired accepts a provided input
        var httpContext = CreateHttpContextWithBody(todo);
        await endpoints[0].RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody);

        // formBodyRequired throws on null input
        httpContext = CreateHttpContextWithBody(null);
        await endpoints[0].RequestDelegate(httpContext);
        Assert.Equal(400, httpContext.Response.StatusCode);

        // formBodyOptional accepts a provided input
        httpContext = CreateHttpContextWithBody(todo);
        await endpoints[1].RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody);

        // formBodyOptional accepts a null input
        httpContext = CreateHttpContextWithBody(null);
        await endpoints[1].RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, string.Empty);
    }

    public static object[][] MapAction_ExplicitHeaderParam_SimpleReturn_Data
    {
        get
        {
            var expectedBody = "Test header value";
            var fromHeaderRequiredSource = """app.MapGet("/", ([FromHeader] string headerValue) => headerValue);""";
            var fromHeaderWithNameRequiredSource = """app.MapGet("/", ([FromHeader(Name = "headerValue")] string parameterName) => parameterName);""";
            var fromHeaderWithNullNameRequiredSource = """app.MapGet("/", ([FromHeader(Name = null)] string headerValue) => headerValue);""";
            var fromHeaderNullableSource = """app.MapGet("/", ([FromHeader] string? headerValue) => headerValue ?? string.Empty);""";
            var fromHeaderDefaultValueSource = """
#nullable disable
string getHeaderWithDefault([FromHeader] string headerValue = null) => headerValue ?? string.Empty;
app.MapGet("/", getHeaderWithDefault);
#nullable restore
""";

            return new[]
            {
                new object[] { fromHeaderRequiredSource, expectedBody, 200, expectedBody },
                new object[] { fromHeaderRequiredSource, null, 400, string.Empty },
                new object[] { fromHeaderWithNameRequiredSource, expectedBody, 200, expectedBody },
                new object[] { fromHeaderWithNameRequiredSource, null, 400, string.Empty },
                new object[] { fromHeaderWithNullNameRequiredSource, expectedBody, 200, expectedBody },
                new object[] { fromHeaderWithNullNameRequiredSource, null, 400, string.Empty },
                new object[] { fromHeaderNullableSource, expectedBody, 200, expectedBody },
                new object[] { fromHeaderNullableSource, null, 200, string.Empty },
                new object[] { fromHeaderDefaultValueSource, expectedBody, 200, expectedBody },
                new object[] { fromHeaderDefaultValueSource, null, 200, string.Empty },
            };
        }
    }

    [Theory]
    [MemberData(nameof(MapAction_ExplicitHeaderParam_SimpleReturn_Data))]
    public async Task MapAction_ExplicitHeaderParam_SimpleReturn(string source, string requestData, int expectedStatusCode, string expectedBody)
    {
        var (_, compilation) = await RunGeneratorAsync(source);
        var endpoint = GetEndpointFromCompilation(compilation);

        var httpContext = CreateHttpContext();
        if (requestData is not null)
        {
            httpContext.Request.Headers["headerValue"] = requestData;
        }

        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody, expectedStatusCode);
    }

    public static object[][] MapAction_ExplicitRouteParam_SimpleReturn_Data
    {
        get
        {
            var expectedBody = "Test route value";
            var fromRouteRequiredSource = """app.MapGet("/{routeValue}", ([FromRoute] string routeValue) => routeValue);""";
            var fromRouteWithNameRequiredSource = """app.MapGet("/{routeValue}", ([FromRoute(Name = "routeValue" )] string parameterName) => parameterName);""";
            var fromRouteWithNullNameRequiredSource = """app.MapGet("/{routeValue}", ([FromRoute(Name = null )] string routeValue) => routeValue);""";
            var fromRouteNullableSource = """app.MapGet("/{routeValue}", ([FromRoute] string? routeValue) => routeValue ?? string.Empty);""";
            var fromRouteDefaultValueSource = """
#nullable disable
string getRouteWithDefault([FromRoute] string routeValue = null) => routeValue ?? string.Empty;
app.MapGet("/{routeValue}", getRouteWithDefault);
#nullable restore
""";

            return new[]
            {
                new object[] { fromRouteRequiredSource, expectedBody, 200, expectedBody },
                new object[] { fromRouteRequiredSource, null, 400, string.Empty },
                new object[] { fromRouteWithNameRequiredSource, expectedBody, 200, expectedBody },
                new object[] { fromRouteWithNameRequiredSource, null, 400, string.Empty },
                new object[] { fromRouteWithNullNameRequiredSource, expectedBody, 200, expectedBody },
                new object[] { fromRouteWithNullNameRequiredSource, null, 400, string.Empty },
                new object[] { fromRouteNullableSource, expectedBody, 200, expectedBody },
                new object[] { fromRouteNullableSource, null, 200, string.Empty },
                new object[] { fromRouteDefaultValueSource, expectedBody, 200, expectedBody },
                new object[] { fromRouteDefaultValueSource, null, 200, string.Empty },
            };
        }
    }

    [Theory]
    [MemberData(nameof(MapAction_ExplicitRouteParam_SimpleReturn_Data))]
    public async Task MapAction_ExplicitRouteParam_SimpleReturn(string source, string requestData, int expectedStatusCode, string expectedBody)
    {
        var (_, compilation) = await RunGeneratorAsync(source);
        var endpoint = GetEndpointFromCompilation(compilation);

        var httpContext = CreateHttpContext();
        if (requestData is not null)
        {
            httpContext.Request.RouteValues["routeValue"] = requestData;
        }

        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody, expectedStatusCode);
    }

    [Fact]
    public async Task MapAction_ExplicitRouteParamWithInvalidName_SimpleReturn()
    {
        var source = $$"""app.MapGet("/{routeValue}", ([FromRoute(Name = "invalidName" )] string parameterName) => parameterName);""";
        var (_, compilation) = await RunGeneratorAsync(source);
        var endpoint = GetEndpointFromCompilation(compilation);

        var httpContext = CreateHttpContext();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => endpoint.RequestDelegate(httpContext));
        Assert.Equal("'invalidName' is not a route parameter.", exception.Message);
    }

    public static object[][] MapAction_RouteOrQueryParam_SimpleReturn_Data
    {
        get
        {
            var expectedBody = "ValueFromRouteOrQuery";
            var implicitRouteRequiredSource = """app.MapGet("/{value}", (string value) => value);""";
            var implicitQueryRequiredSource = """app.MapGet("", (string value) => value);""";
            var implicitRouteNullableSource = """app.MapGet("/{value}", (string? value) => value ?? string.Empty);""";
            var implicitQueryNullableSource = """app.MapGet("/", (string? value) => value ?? string.Empty);""";
            var implicitRouteDefaultValueSource = """
#nullable disable
string getRouteWithDefault(string value = null) => value ?? string.Empty;
app.MapGet("/{value}", getRouteWithDefault);
#nullable restore
""";

            var implicitQueryDefaultValueSource = """
#nullable disable
string getQueryWithDefault(string value = null) => value ?? string.Empty;
app.MapGet("/", getQueryWithDefault);
#nullable restore
""";

            return new[]
            {
                new object[] { implicitRouteRequiredSource, true, false, 200, expectedBody },
                new object[] { implicitRouteRequiredSource, false, false, 400, string.Empty },
                new object[] { implicitQueryRequiredSource, false, true, 200, expectedBody },
                new object[] { implicitQueryRequiredSource, false, false, 400, string.Empty },

                new object[] { implicitRouteNullableSource, true, false, 200, expectedBody },
                new object[] { implicitRouteNullableSource, false, false, 200, string.Empty },
                new object[] { implicitQueryNullableSource, false, true, 200, expectedBody },
                new object[] { implicitQueryNullableSource, false, false, 200, string.Empty },

                new object[] { implicitRouteDefaultValueSource, true, false, 200, expectedBody },
                new object[] { implicitRouteDefaultValueSource, false, false, 200, string.Empty },
                new object[] { implicitQueryDefaultValueSource, false, true, 200, expectedBody },
                new object[] { implicitQueryDefaultValueSource, false, false, 200, string.Empty },
            };
        }
    }

    [Theory]
    [MemberData(nameof(MapAction_RouteOrQueryParam_SimpleReturn_Data))]
    public async Task MapAction_RouteOrQueryParam_SimpleReturn(string source, bool hasRoute, bool hasQuery, int expectedStatusCode, string expectedBody)
    {
        var (_, compilation) = await RunGeneratorAsync(source);
        var endpoint = GetEndpointFromCompilation(compilation);

        var httpContext = CreateHttpContext();
        if (hasRoute)
        {
            httpContext.Request.RouteValues["value"] = expectedBody;
        }

        if (hasQuery)
        {
            httpContext.Request.QueryString = new QueryString($"?value={expectedBody}");
        }

        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody, expectedStatusCode);
    }

    [Fact]
    public async Task MapAction_UnknownParameter_EmitsDiagnostic_NoSource()
    {
        // This will eventually be handled by the EndpointParameterSource.JsonBodyOrService.
        // All parameters should theoretically be handleable with enough "Or"s in the future
        // we'll remove this test and diagnostic.
        var source = """
app.MapGet("/", (IServiceProvider provider) => "Hello world!");
""";
        var expectedBody = "Hello world!";
        var (result, compilation) = await RunGeneratorAsync(source);

        // Emits diagnostic but generates no source
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticDescriptors.GetUnableToResolveParameterDescriptor("provider").Id, diagnostic.Id);
        Assert.Empty(result.GeneratedSources);

        // Falls back to runtime-generated endpoint
        var endpoint = GetEndpointFromCompilation(compilation, expectSourceKey: false);

        var httpContext = CreateHttpContext();
        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody);
    }

    public static object[][] MapAction_ExplicitServiceParam_SimpleReturn_Data
    {
        get
        {
            var fromServiceRequiredSource = """app.MapPost("/", ([FromServices]TestService svc) => svc.TestServiceMethod());""";
            var fromServiceNullableSource = """app.MapPost("/", ([FromServices]TestService? svc) => svc?.TestServiceMethod() ?? string.Empty);""";
            var fromServiceDefaultValueSource = """
#nullable disable
string postServiceWithDefault([FromServices]TestService svc = null) => svc?.TestServiceMethod() ?? string.Empty;
app.MapPost("/", postServiceWithDefault);
#nullable restore
""";

            var fromServiceEnumerableRequiredSource = """app.MapPost("/", ([FromServices]IEnumerable<TestService>  svc) => svc.FirstOrDefault()?.TestServiceMethod() ?? string.Empty);""";
            var fromServiceEnumerableNullableSource = """app.MapPost("/", ([FromServices]IEnumerable<TestService>? svc) => svc?.FirstOrDefault()?.TestServiceMethod() ?? string.Empty);""";
            var fromServiceEnumerableDefaultValueSource = """
#nullable disable
string postServiceWithDefault([FromServices]IEnumerable<TestService> svc = null) => svc?.FirstOrDefault()?.TestServiceMethod() ?? string.Empty;
app.MapPost("/", postServiceWithDefault);
#nullable restore
""";

            return new[]
            {
                new object[] { fromServiceRequiredSource, true, true },
                new object[] { fromServiceRequiredSource, false, false },
                new object[] { fromServiceNullableSource, true, true },
                new object[] { fromServiceNullableSource, false, true },
                new object[] { fromServiceDefaultValueSource, true, true },
                new object[] { fromServiceDefaultValueSource, false, true },
                new object[] { fromServiceEnumerableRequiredSource, true, true },
                new object[] { fromServiceEnumerableRequiredSource, false, true },
                new object[] { fromServiceEnumerableNullableSource, true, true },
                new object[] { fromServiceEnumerableNullableSource, false, true },
                new object[] { fromServiceEnumerableDefaultValueSource, true, true },
                new object[] { fromServiceEnumerableDefaultValueSource, false, true }
            };
        }
    }

    [Theory]
    [MemberData(nameof(MapAction_ExplicitServiceParam_SimpleReturn_Data))]
    public async Task MapAction_ExplicitServiceParam_SimpleReturn(string source, bool hasService, bool isValid)
    {
        var (_, compilation) = await RunGeneratorAsync(source);
        var endpoint = GetEndpointFromCompilation(compilation);

        var httpContext = CreateHttpContext();
        if (hasService)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<TestService>(new TestService());
            var services = serviceCollection.BuildServiceProvider();
            httpContext.RequestServices = services;
        }

        if (isValid)
        {
            await endpoint.RequestDelegate(httpContext);
            await VerifyResponseBodyAsync(httpContext, hasService ? "Produced from service!" : string.Empty);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => endpoint.RequestDelegate(httpContext));
            Assert.False(httpContext.RequestAborted.IsCancellationRequested);
        }
    }

    [Fact]
    public async Task MapAction_ExplicitServiceParam_SimpleReturn_Snapshot()
    {
        var source = """
app.MapGet("/fromServiceRequired", ([FromServices]TestService svc) => svc.TestServiceMethod());
app.MapGet("/enumerableFromService", ([FromServices]IEnumerable<TestService> svc) => svc?.FirstOrDefault()?.TestServiceMethod() ?? string.Empty);
app.MapGet("/multipleFromService", ([FromServices]TestService? svc, [FromServices]IEnumerable<TestService> svcs) =>
    $"{(svcs?.FirstOrDefault()?.TestServiceMethod() ?? string.Empty)}, {svc?.TestServiceMethod()}");
""";
        var httpContext = CreateHttpContext();
        var expectedBody = "Produced from service!";
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<TestService>(new TestService());
        var services = serviceCollection.BuildServiceProvider();
        var emptyServices = new ServiceCollection().BuildServiceProvider();

        var (_, compilation) = await RunGeneratorAsync(source);

        await VerifyAgainstBaselineUsingFile(compilation);

        var endpoints = GetEndpointsFromCompilation(compilation);

        Assert.Equal(3, endpoints.Length);

        // fromServiceRequired throws on null input
        httpContext.RequestServices = emptyServices;
        await Assert.ThrowsAsync<InvalidOperationException>(() => endpoints[0].RequestDelegate(httpContext));
        Assert.False(httpContext.RequestAborted.IsCancellationRequested);

        // fromServiceRequired accepts a provided input
        httpContext = CreateHttpContext();
        httpContext.RequestServices = services;
        await endpoints[0].RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody);

        // enumerableFromService
        httpContext = CreateHttpContext();
        httpContext.RequestServices = services;
        await endpoints[1].RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody);

        // multipleFromService
        httpContext = CreateHttpContext();
        httpContext.RequestServices = services;
        await endpoints[2].RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, $"{expectedBody}, {expectedBody}");
    }

    [Fact]
    public async Task MapAction_ExplicitSource_SimpleReturn_Snapshot()
    {
        var source = """
app.MapGet("/fromQuery", ([FromQuery] string queryValue) => queryValue ?? string.Empty);
app.MapGet("/fromHeader", ([FromHeader] string headerValue) => headerValue ?? string.Empty);
app.MapGet("/fromRoute/{routeValue}", ([FromRoute] string routeValue) => routeValue ?? string.Empty);
app.MapGet("/fromRouteRequiredImplicit/{value}", (string value) => value);
app.MapGet("/fromQueryRequiredImplicit", (string value) => value);
""";
        var (_, compilation) = await RunGeneratorAsync(source);

        await VerifyAgainstBaselineUsingFile(compilation);
    }

    [Fact]
    public async Task MapAction_RequestDelegateHandler_DoesNotEmit()
    {
        var source = """
app.MapGet("/", (HttpContext context) => context.Response.WriteAsync("Hello world"));
""";
        var (result, _) = await RunGeneratorAsync(source);
        var endpointModels = GetStaticEndpoints(result, GeneratorSteps.EndpointModelStep);

        Assert.Empty(result.GeneratedSources);
        Assert.Empty(endpointModels);
    }
}
