// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// An <see cref="IResult"/> that returns a Found (302), Moved Permanently (301), Temporary Redirect (307),
/// or Permanent Redirect (308) response with a Location header.
/// Targets a registered route.
/// </summary>
public sealed partial class RedirectToRouteHttpResult : IResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RedirectToRouteHttpResult"/> with the values
    /// provided.
    /// </summary>
    /// <param name="routeValues">The parameters for the route.</param>
    public RedirectToRouteHttpResult(object routeValues)
        : this(routeName: null, routeValues)
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedirectToRouteHttpResult"/> with the values
    /// provided.
    /// </summary>
    /// <param name="routeName">The name of the route.</param>
    public RedirectToRouteHttpResult(string routeName)
        : this(routeName, routeValues: null)
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedirectToRouteHttpResult"/> with the values
    /// provided.
    /// </summary>
    /// <param name="routeName">The name of the route.</param>
    /// <param name="routeValues">The parameters for the route.</param>
    public RedirectToRouteHttpResult(string? routeName, object? routeValues)
    {
        RouteName = routeName;
        RouteValues = routeValues == null ? null : new RouteValueDictionary(routeValues);
    }

    /// <summary>
    /// Gets the name of the route to use for generating the URL.
    /// </summary>
    public string? RouteName { get; }

    /// <summary>
    /// Gets the route data to use for generating the URL.
    /// </summary>
    public RouteValueDictionary? RouteValues { get; }

    /// <summary>
    /// Gets the value that specifies that the redirect should be permanent if true or temporary if false.
    /// </summary>
    public bool Permanent { get; init; }

    /// <summary>
    /// Gets an indication that the redirect preserves the initial request method.
    /// </summary>
    public bool PreserveMethod { get; init; }

    /// <summary>
    /// Gets the fragment to add to the URL.
    /// </summary>
    public string? Fragment { get; init; }

    /// <inheritdoc />
    public Task ExecuteAsync(HttpContext httpContext)
    {
        var linkGenerator = httpContext.RequestServices.GetRequiredService<LinkGenerator>();

        var destinationUrl = linkGenerator.GetUriByRouteValues(
            httpContext,
            RouteName,
            RouteValues,
            fragment: Fragment == null ? FragmentString.Empty : new FragmentString("#" + Fragment));

        if (string.IsNullOrEmpty(destinationUrl))
        {
            throw new InvalidOperationException("No route matches the supplied values.");
        }

        // Creating the logger with a string to preserve the category after the refactoring.
        var loggerFactory = httpContext.RequestServices.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("Microsoft.AspNetCore.Http.Result.RedirectToRouteResult");
        Log.RedirectToRouteResultExecuting(logger, destinationUrl, RouteName);

        if (PreserveMethod)
        {
            httpContext.Response.StatusCode = Permanent ?
                StatusCodes.Status308PermanentRedirect : StatusCodes.Status307TemporaryRedirect;
            httpContext.Response.Headers.Location = destinationUrl;
        }
        else
        {
            httpContext.Response.Redirect(destinationUrl, Permanent);
        }

        return Task.CompletedTask;
    }

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Information,
            "Executing RedirectToRouteResult, redirecting to {Destination} from route {RouteName}.",
            EventName = "RedirectToRouteResultExecuting")]
        public static partial void RedirectToRouteResultExecuting(ILogger logger, string destination, string? routeName);
    }
}
