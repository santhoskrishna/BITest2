// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Identity;

/// <summary>
/// Provides an abstraction for a store which stores info about user's authenticator.
/// </summary>
/// <typeparam name="TUser">The type encapsulating a user.</typeparam>
public interface IUserAuthenticatorKeyStore<TUser> : IUserStore<TUser> where TUser : class
{
    /// <summary>
    /// Sets the authenticator key for the specified <paramref name="user"/>.
    /// </summary>
    /// <param name="user">The user whose authenticator key should be set.</param>
    /// <param name="key">The authenticator key to set.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
    Task SetAuthenticatorKeyAsync(TUser user, string key, CancellationToken cancellationToken);

    /// <summary>
    /// Get the authenticator key for the specified <paramref name="user" />.
    /// </summary>
    /// <param name="user">The user whose security stamp should be retrieved.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the security stamp for the specified <paramref name="user"/>.</returns>
    Task<string?> GetAuthenticatorKeyAsync(TUser user, CancellationToken cancellationToken);

    /// <summary>
    /// Get the last verified authenticator timestamp for the specified <paramref name="user" />.
    /// </summary>
    /// <param name="user">The user whose authenticator timestamp should be retrieved.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>
    /// The <see cref="Task"/> that represents the asynchronous operation, containing the last verified authenticator timestamp for the specified <paramref name="user"/>,
    /// or null, when the authenticator code has never been verified.</returns>
    Task<long?> GetAuthenticatorTimestampAsync(TUser user, CancellationToken cancellationToken);

    /// <summary>
    /// Set the last verified authenticator timestamp for the specified <paramref name="user" />.
    /// </summary>
    /// <param name="user">The user whose authenticator timestamp should be set.</param>
    /// <param name="timestamp">The new timestamp value to be set.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>
    /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the security stamp for the specified <paramref name="user"/>.</returns>
    Task SetAuthenticatorTimestampAsync(TUser user, long timestamp, CancellationToken cancellationToken);
}
