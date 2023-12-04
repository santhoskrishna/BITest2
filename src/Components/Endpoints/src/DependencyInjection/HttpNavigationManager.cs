// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Components.Routing;

namespace Microsoft.AspNetCore.Components.Endpoints;

internal sealed class HttpNavigationManager : NavigationManager, IHostEnvironmentNavigationManager
{
    void IHostEnvironmentNavigationManager.Initialize(string baseUri, string uri) => Initialize(baseUri, uri);

    protected override void NavigateToCore(string uri, NavigationOptions options)
    {
        var absoluteUriString = ToAbsoluteUri(uri).ToString();
        throw options.ReplaceHistoryEntry
            ? new ReplaceHistoryNavigationException(ToAbsoluteUri(absoluteUriString).ToString())
            : new NavigationException(ToAbsoluteUri(absoluteUriString).ToString());
    }
}
