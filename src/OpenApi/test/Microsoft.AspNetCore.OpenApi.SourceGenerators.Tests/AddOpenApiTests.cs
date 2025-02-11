// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.OpenApi.SourceGenerators.Tests;

[UsesVerify]
public class AddOpenApiTests
{
    [Fact]
    public async Task CanInterceptAddOpenApi()
    {
        var source = """
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder();

// No parameters
builder.Services.AddOpenApi();
// Name parameter
builder.Services.AddOpenApi("v2");
// Configure options parameter
builder.Services.AddOpenApi(options =>
{
    options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi2_0;
});
// Name and configure options parameters
builder.Services.AddOpenApi("v2", options =>
{
    options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi2_0;
});

var app = builder.Build();

app.MapPost("", () => "Hello world!");

app.Run();
""";

        var generator = new XmlCommentGenerator();
        await SnapshotTestHelper.Verify(source, generator, out var compilation);
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity > DiagnosticSeverity.Warning));
    }
}
