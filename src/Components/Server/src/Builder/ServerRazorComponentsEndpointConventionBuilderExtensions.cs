// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Components.Endpoints.Infrastructure;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Interactive server specific endpoint conventions for razor component applications.
/// </summary>
public static class ServerRazorComponentsEndpointConventionBuilderExtensions
{
    /// <summary>
    /// Configures the application to support the <see cref="RenderMode.InteractiveServer"/> render mode.
    /// </summary>
    /// <returns>The <see cref="RazorComponentsEndpointConventionBuilder"/>.</returns>
    public static RazorComponentsEndpointConventionBuilder AddInteractiveServerRenderMode(this RazorComponentsEndpointConventionBuilder builder)
    {
        return AddInteractiveServerRenderMode(builder, (_) => { });
    }

    /// <summary>
    /// Maps the Blazor <see cref="Hub" /> to the default path.
    /// </summary>
    /// <param name="builder">The <see cref="RazorComponentsEndpointConventionBuilder"/>.</param>
    /// <param name="configure">A callback to configure server endpoint options.</param>
    /// <returns>The <see cref="ComponentEndpointConventionBuilder"/>.</returns>
    public static RazorComponentsEndpointConventionBuilder AddInteractiveServerRenderMode(
        this RazorComponentsEndpointConventionBuilder builder,
        Action<ServerComponentsEndpointOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new ServerComponentsEndpointOptions();
        configure.Invoke(options);

        ComponentEndpointConventionBuilderHelper.AddRenderMode(builder, new InternalServerRenderMode(options));

        if ((options.ConfigureWebSocketAcceptContext is not null || !options.DisableWebSocketCompression) &&
            options.ContentSecurityFrameAncestorsPolicy != null)
        {
            builder.Add(b =>
            {
                for (var i = 0; i < b.Metadata.Count; i++)
                {
                    var metadata = b.Metadata[i];
                    if (metadata is ComponentTypeMetadata)
                    {
                        var original = b.RequestDelegate;
                        b.RequestDelegate = async context =>
                        {
                            if (context.Response.Headers.ContentSecurityPolicy.Count == 0)
                            {
                                context.Response.Headers.ContentSecurityPolicy = $"frame-ancestors {options.ContentSecurityFrameAncestorsPolicy}";
                            }
                            else
                            {
                                var result = new string[context.Response.Headers.ContentSecurityPolicy.Count + 1];
                                for (var i = 0; i < result.Length - 1; i++)
                                {
                                    result[i] = context.Response.Headers.ContentSecurityPolicy[i];
                                }
                                result[^1] = $"frame-ancestors {options.ContentSecurityFrameAncestorsPolicy}";
                                context.Response.Headers.ContentSecurityPolicy = result;
                            }
                            await original(context);
                        };
                    }
                }
            });
        }

        return builder;
    }
}
