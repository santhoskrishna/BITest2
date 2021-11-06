// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Mvc;

/// <summary>
/// An <see cref="ActionResult"/> that on execution invokes <see cref="M:HttpContext.SignInAsync"/>.
/// </summary>
public class SignInResult : ActionResult
{
    /// <summary>
    /// Initializes a new instance of <see cref="SignInResult"/> with the
    /// default authentication scheme.
    /// </summary>
    /// <param name="principal">The claims principal containing the user claims.</param>
    public SignInResult(ClaimsPrincipal principal)
        : this(authenticationScheme: null, principal, properties: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="SignInResult"/> with the
    /// specified authentication scheme.
    /// </summary>
    /// <param name="authenticationScheme">The authentication scheme to use when signing in the user.</param>
    /// <param name="principal">The claims principal containing the user claims.</param>
    public SignInResult(string? authenticationScheme, ClaimsPrincipal principal)
        : this(authenticationScheme, principal, properties: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="SignInResult"/> with the
    /// default authentication scheme and <paramref name="properties"/>.
    /// </summary>
    /// <param name="principal">The claims principal containing the user claims.</param>
    /// <param name="properties"><see cref="AuthenticationProperties"/> used to perform the sign-in operation.</param>
    public SignInResult(ClaimsPrincipal principal, AuthenticationProperties? properties)
        : this(authenticationScheme: null, principal, properties)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="SignInResult"/> with the
    /// specified authentication scheme and <paramref name="properties"/>.
    /// </summary>
    /// <param name="authenticationScheme">The authentication schemes to use when signing in the user.</param>
    /// <param name="principal">The claims principal containing the user claims.</param>
    /// <param name="properties"><see cref="AuthenticationProperties"/> used to perform the sign-in operation.</param>
    public SignInResult(string? authenticationScheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
    {
        Principal = principal ?? throw new ArgumentNullException(nameof(principal));
        AuthenticationScheme = authenticationScheme;
        Properties = properties;
    }

    /// <summary>
    /// Gets or sets the authentication scheme that is used to perform the sign-in operation.
    /// </summary>
    public string? AuthenticationScheme { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="ClaimsPrincipal"/> containing the user claims.
    /// </summary>
    public ClaimsPrincipal Principal { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="AuthenticationProperties"/> used to perform the sign-in operation.
    /// </summary>
    public AuthenticationProperties? Properties { get; set; }

    /// <inheritdoc />
    public override Task ExecuteResultAsync(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var httpContext = context.HttpContext;
        var loggerFactory = httpContext.RequestServices.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<SignInResult>();

        logger.SignInResultExecuting(AuthenticationScheme, Principal);

        return httpContext.SignInAsync(AuthenticationScheme, Principal, Properties);
    }
}
