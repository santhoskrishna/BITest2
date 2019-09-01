// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Microsoft.AspNetCore.Mvc
{
    /// <summary>
    /// A <see cref="StatusCodeResult"/> that when
    /// executed will produce an Internal Server Error (500) response.
    /// </summary>
    [DefaultStatusCode(DefaultStatusCode)]
    public class InternalServerErrorResult : StatusCodeResult
    {
        private const int DefaultStatusCode = StatusCodes.Status500InternalServerError;

        /// <summary>
        /// Creates a new <see cref="InternalServerErrorResult"/> instance.
        /// </summary>
        public InternalServerErrorResult()
            : base(DefaultStatusCode)
        {
        }
    }
}
