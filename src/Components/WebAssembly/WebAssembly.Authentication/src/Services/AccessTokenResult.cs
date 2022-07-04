// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Components.WebAssembly.Authentication;

/// <summary>
/// Represents the result of trying to provision an access token.
/// </summary>
public class AccessTokenResult
{
    private readonly AccessToken _token;

    /// <summary>
    /// Initializes a new instance of <see cref="AccessTokenResult"/>.
    /// </summary>
    /// <param name="status">The status of the result.</param>
    /// <param name="token">The <see cref="AccessToken"/> in case it was successful.</param>
    /// <param name="redirectUrl">The redirect uri to go to for provisioning the token.</param>
    public AccessTokenResult(AccessTokenResultStatus status, AccessToken token, string redirectUrl)
    {
        Status = status;
        _token = token;
        RedirectUrl = redirectUrl;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="AccessTokenResult"/>.
    /// </summary>
    /// <param name="status">The status of the result.</param>
    /// <param name="token">The <see cref="AccessToken"/> in case it was successful.</param>
    /// <param name="interactiveRequestUrl">The redirect uri to go to for provisioning the token with <see cref="NavigationManagerExtensions.NavigateToLogin(NavigationManager, string, InteractiveAuthenticationRequest)"/>.</param>
    /// <param name="interactiveRequest">The <see cref="InteractiveAuthenticationRequest"/> containing the parameters for the interactive authentication.</param>
    public AccessTokenResult(AccessTokenResultStatus status, AccessToken token, string interactiveRequestUrl, InteractiveAuthenticationRequest interactiveRequest)
    {
        Status = status;
        _token = token;
        InteractiveRequestUrl = interactiveRequestUrl;
        InteractiveRequest = interactiveRequest;
    }

    /// <summary>
    /// Gets the status of the current operation. See <see cref="AccessTokenResultStatus"/> for a list of statuses.
    /// </summary>
    public AccessTokenResultStatus Status { get; }

    /// <summary>
    /// Gets the URL to redirect to if <see cref="Status"/> is <see cref="AccessTokenResultStatus.RequiresRedirect"/>.
    /// </summary>
    public string RedirectUrl { get; }

    /// <summary>
    /// Gets the URL to call <see cref="NavigationManagerExtensions.NavigateToLogin(NavigationManager, string, InteractiveAuthenticationRequest)"/> if <see cref="Status"/> is
    /// <see cref="AccessTokenResultStatus.RequiresRedirect"/>.
    /// </summary>
    public string InteractiveRequestUrl { get; }

    /// <summary>
    /// Gets the <see cref="InteractiveAuthenticationRequest"/> to use if <see cref="Status"/> is <see cref="AccessTokenResultStatus.RequiresRedirect"/>.
    /// </summary>
    public InteractiveAuthenticationRequest InteractiveRequest { get; }

    /// <summary>
    /// Determines whether the token request was successful and makes the <see cref="AccessToken"/> available for use when it is.
    /// </summary>
    /// <param name="accessToken">The <see cref="AccessToken"/> if the request was successful.</param>
    /// <returns><c>true</c> when the token request is successful; <c>false</c> otherwise.</returns>
    public bool TryGetToken(out AccessToken accessToken)
    {
        if (Status == AccessTokenResultStatus.Success)
        {
            accessToken = _token;
            return true;
        }
        else
        {
            accessToken = null;
            return false;
        }
    }
}
