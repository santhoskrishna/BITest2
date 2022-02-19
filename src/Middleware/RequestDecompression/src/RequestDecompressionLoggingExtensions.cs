// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.RequestDecompression;

internal static partial class RequestDecompressionLoggingExtensions
{
    [LoggerMessage(1, LogLevel.Trace, "The Content-Encoding header is missing or empty. Skipping request decompression.", EventName = "NoContentEncoding")]
    public static partial void NoContentEncoding(this ILogger logger);

    [LoggerMessage(2, LogLevel.Debug, "Request decompression is not supported for multiple Content-Encodings.", EventName = "MultipleContentEncodingsSpecified")]
    public static partial void MultipleContentEncodingsSpecified(this ILogger logger);

    [LoggerMessage(3, LogLevel.Debug, "No matching request decompression provider found.", EventName = "NoDecompressionProvider")]
    public static partial void NoDecompressionProvider(this ILogger logger);

    [LoggerMessage(4, LogLevel.Debug, "The request will be decompressed with '{provider}'.", EventName = "DecompressingWith")]
    public static partial void DecompressingWith(this ILogger logger, string provider);
}
