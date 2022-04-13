// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Internal;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Http.HttpResults;

public class HttpFileStreamResultTests : FileStreamResultTestBase
{
    protected override Task ExecuteAsync(
        HttpContext httpContext,
        Stream stream,
        string contentType,
        DateTimeOffset? lastModified = null,
        EntityTagHeaderValue entityTag = null,
        bool enableRangeProcessing = false)
    {
        var fileStreamResult = new FileStreamHttpResult(stream, contentType)
        {
            LastModified = lastModified,
            EntityTag = entityTag,
            EnableRangeProcessing = enableRangeProcessing
        };

        return fileStreamResult.ExecuteAsync(httpContext);
    }

    [Fact]
    public void Constructor_SetsFileName()
    {
        // Arrange
        var stream = Stream.Null;

        // Act
        var result = new FileStreamHttpResult(stream, "text/plain");

        // Assert
        Assert.Equal(stream, result.FileStream);
    }

    [Fact]
    public void Constructor_SetsContentTypeAndParameters()
    {
        // Arrange
        var stream = Stream.Null;
        var contentType = "text/plain; charset=us-ascii; p1=p1-value";
        var expectedMediaType = contentType;

        // Act
        var result = new FileStreamHttpResult(stream, contentType);

        // Assert
        Assert.Equal(stream, result.FileStream);
        Assert.Equal(expectedMediaType, result.ContentType);
    }

    [Fact]
    public void Constructor_SetsLastModifiedAndEtag()
    {
        // Arrange
        var stream = Stream.Null;
        var contentType = "text/plain";
        var expectedMediaType = contentType;
        var lastModified = new DateTimeOffset();
        var entityTag = new EntityTagHeaderValue("\"Etag\"");

        // Act
        var result = new FileStreamHttpResult(stream, contentType)
        {
            LastModified = lastModified,
            EntityTag = entityTag,
        };

        // Assert
        Assert.Equal(lastModified, result.LastModified);
        Assert.Equal(entityTag, result.EntityTag);
        Assert.Equal(expectedMediaType, result.ContentType);
    }

}
