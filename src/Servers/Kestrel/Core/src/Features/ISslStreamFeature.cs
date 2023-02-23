// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Security;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Features;

/// <summary>
/// Feature to set access the TLS application protocol
/// </summary>
public interface ISslStreamFeature
{
    /// <summary>
    /// Gets the <see cref="SslStream"/>.
    /// </summary>
    SslStream SslStream { get; }
}
