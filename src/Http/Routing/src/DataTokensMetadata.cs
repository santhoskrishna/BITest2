// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Routing;

/// <summary>
/// Metadata that defines data tokens for an <see cref="Endpoint"/>. This metadata
/// type provides data tokens value for <see cref="RouteData.DataTokens"/> associated
/// with an endpoint.
/// </summary>
public sealed class DataTokensMetadata : IDataTokensMetadata
{
    /// <summary>
    /// Constructor for a new <see cref="DataTokensMetadata"/> given <paramref name="dataTokens"/>.
    /// </summary>
    /// <param name="dataTokens">The data tokens.</param>
    public DataTokensMetadata(IReadOnlyDictionary<string, object?> dataTokens)
    {
        DataTokens = dataTokens ?? throw new ArgumentNullException(nameof(dataTokens));
    }

    /// <summary>
    /// Get the data tokens.
    /// </summary>
    public IReadOnlyDictionary<string, object?> DataTokens { get; }

    /// <inheritdoc/>
    public override string ToString()
    {
        var metadataTokensBuilder = new StringBuilder();
        foreach(var DataToken in DataTokens)
        {
            metadataTokensBuilder.AppendFormat(CultureInfo.InvariantCulture, "{0} ={1}", DataToken.Key, DataToken.Value);
            metadataTokensBuilder.AppendLine();
        }
            return metadataTokensBuilder.ToString();
    }
}
