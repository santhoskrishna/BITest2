// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for <see cref="ISignalRBuilder"/>.
    /// </summary>
    public static class JsonProtocolDependencyInjectionExtensions
    {
        /// <summary>
        /// Enables the JSON protocol for SignalR.
        /// </summary>
        /// <remarks>
        /// This has no effect if the JSON protocol has already been enabled.
        /// </remarks>
        /// <param name="builder">The <see cref="ISignalRBuilder"/> representing the SignalR server to add JSON protocol support to.</param>
        /// <returns>The value of <paramref name="builder"/></returns>
        public static TBuilder AddJsonProtocol<TBuilder>(this TBuilder builder) where TBuilder : ISignalRBuilder
        {
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHubProtocol, JsonHubProtocol>());
            return builder;
        }
    }
}
