// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Http
{
    public class DefaultHttpContextFactory : IHttpContextFactory
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly FormOptions _formOptions;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        // This takes the IServiceProvider because it needs to support an ever expanding
        // set of services that flow down into HttpContext features
        public DefaultHttpContextFactory(IServiceProvider serviceProvider)
        {
            // May be null
            _httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>();
            _formOptions = serviceProvider.GetRequiredService<IOptions<FormOptions>>().Value;
            _serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        }

        public HttpContext Create(IFeatureCollection featureCollection)
        {
            if (featureCollection == null)
            {
                throw new ArgumentNullException(nameof(featureCollection));
            }

            var httpContext = new DefaultHttpContext(featureCollection);
            return Initialize(httpContext);
        }

        public DefaultHttpContext CreateOrInitialize(DefaultHttpContext httpContext, IFeatureCollection featureCollection)
        {
            if (featureCollection == null)
            {
                throw new ArgumentNullException(nameof(featureCollection));
            }

            if (httpContext is null)
            {
                httpContext = new DefaultHttpContext(featureCollection);
            }
            else
            {
                httpContext.Initialize(featureCollection);
            }

            return Initialize(httpContext);
        }

        private DefaultHttpContext Initialize(DefaultHttpContext httpContext)
        {
            if (_httpContextAccessor != null)
            {
                _httpContextAccessor.HttpContext = httpContext;
            }

            httpContext.FormOptions = _formOptions;
            httpContext.ServiceScopeFactory = _serviceScopeFactory;

            return httpContext;
        }

        public void Dispose(HttpContext httpContext)
        {
            if (_httpContextAccessor != null)
            {
                _httpContextAccessor.HttpContext = null;
            }
        }

        public void Dispose(DefaultHttpContext httpContext)
        {
            if (_httpContextAccessor != null)
            {
                _httpContextAccessor.HttpContext = null;
            }

            httpContext.Uninitialize();
        }
    }
}
