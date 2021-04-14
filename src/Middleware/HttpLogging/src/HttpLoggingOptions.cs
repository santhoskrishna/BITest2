// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.HttpLogging
{
    /// <summary>
    /// Options for the <see cref="HttpLoggingMiddleware"/>
    /// </summary>
    public class HttpLoggingOptions
    {
        /// <summary>
        /// Fields to log for the Request and Response. Defaults to logging request and response properties and headers.
        /// </summary>
        public HttpLoggingFields LoggingFields { get; set; } = HttpLoggingFields.RequestPropertiesAndHeaders | HttpLoggingFields.ResponsePropertiesAndHeaders;

        /// <summary>
        /// Request header values that are allowed to be logged.
        /// </summary>
        /// <remarks>
        /// If a request header is not present in the <see cref="AllowedRequestHeaders"/>,
        /// the header name will be logged with a redacted value.
        /// </remarks>
        public ISet<string> AllowedRequestHeaders { get; } = new HashSet<string>()
        {
            HeaderNames.Accept,
            HeaderNames.AcceptEncoding,
            HeaderNames.AcceptLanguage,
            HeaderNames.Allow,
            HeaderNames.Connection,
            HeaderNames.ContentType,
            HeaderNames.Host,
            HeaderNames.UserAgent
        };

        /// <summary>
        /// Response header values that are allowed to be logged.
        /// </summary>
        /// <remarks>
        /// If a response header is not present in the <see cref="AllowedResponseHeaders"/>,
        /// the header name will be logged with a redacted value.
        /// </remarks>
        public ISet<string> AllowedResponseHeaders { get; } = new HashSet<string>()
        {
            HeaderNames.ContentType,
            HeaderNames.Date,
            HeaderNames.ETag,
            HeaderNames.Server,
            HeaderNames.TransferEncoding
        };

        /// <summary>
        /// The encoding to log the request body and response body, if the content-type of the body
        /// is one of the <see cref="SupportedMediaTypes"/>.
        /// </summary>
        public Encoding? BodyEncoding { get; set; } = Encoding.UTF8;

        /// <summary>
        /// A list of supported media type values for request and response body logging.
        /// </summary>
        /// <remarks>
        /// If the request or response do not match the supported media type, the response body will not be logged.
        /// </remarks>
        public List<MediaTypeHeaderValue>? SupportedMediaTypes { get; } = new List<MediaTypeHeaderValue>()
        {
            new MediaTypeHeaderValue("application/json"),
            new MediaTypeHeaderValue("application/xml"),
            new MediaTypeHeaderValue("text/*")
        };

        /// <summary>
        /// Maximum request body size to log (in bytes). Defaults to 32 KB.
        /// </summary>
        public int RequestBodyLogLimit { get; set; } = 32 * 1024;

        /// <summary>
        /// Timeout for reading requset body to log.
        /// </summary>
        public TimeSpan RequestBodyTimeout { get; set; } = Debugger.IsAttached ? TimeSpan.FromDays(1) : TimeSpan.FromSeconds(1);

        /// <summary>
        /// Maximum response body size to log (in bytes). Defaults to 32 KB.
        /// </summary>
        public int ResponseBodyLogLimit { get; set; } = 32 * 1024; // 32KB
    }
}
