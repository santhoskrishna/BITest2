// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Configuration;

namespace Microsoft.AspNetCore.Server.Kestrel;

/// <summary>
/// Configuration loader for Kestrel.
/// </summary>
public class KestrelConfigurationLoader
{
    private readonly IHttpsConfigurationService _httpsConfigurationService;

    internal bool IsLoaded { get; private set; }

    internal KestrelConfigurationLoader(
        KestrelServerOptions options,
        IConfiguration configuration,
        IHttpsConfigurationService httpsConfigurationService,
        bool reloadOnChange)
    {
        Options = options;
        Configuration = configuration;

        ReloadOnChange = reloadOnChange;

        ConfigurationReader = new ConfigurationReader(configuration);

        _httpsConfigurationService = httpsConfigurationService;
    }

    /// <summary>
    /// Gets the <see cref="KestrelServerOptions"/>.
    /// </summary>
    public KestrelServerOptions Options { get; }

    /// <summary>
    /// Gets the application <see cref="IConfiguration"/>.
    /// </summary>
    public IConfiguration Configuration { get; internal set; }

    /// <summary>
    /// If <see langword="true" />, Kestrel will dynamically update endpoint bindings when configuration changes.
    /// This will only reload endpoints defined in the "Endpoints" section of your Kestrel configuration. Endpoints defined in code will not be reloaded.
    /// </summary>
    internal bool ReloadOnChange { get; }

    private ConfigurationReader ConfigurationReader { get; set; }

    private IDictionary<string, Action<EndpointConfiguration>> EndpointConfigurations { get; }
        = new Dictionary<string, Action<EndpointConfiguration>>(0, StringComparer.OrdinalIgnoreCase);

    // Actions that will be delayed until Load so that they aren't applied if the configuration loader is replaced.
    private IList<Action> EndpointsToAdd { get; } = new List<Action>();

    private CertificateConfig? DefaultCertificateConfig { get; set; }
    internal X509Certificate2? DefaultCertificate { get; set; }

    /// <summary>
    /// Specifies a configuration Action to run when an endpoint with the given name is loaded from configuration.
    /// </summary>
    public KestrelConfigurationLoader Endpoint(string name, Action<EndpointConfiguration> configureOptions)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentNullException(nameof(name));
        }

        EndpointConfigurations[name] = configureOptions ?? throw new ArgumentNullException(nameof(configureOptions));
        return this;
    }

    /// <summary>
    /// Bind to given IP address and port.
    /// </summary>
    public KestrelConfigurationLoader Endpoint(IPAddress address, int port) => Endpoint(address, port, _ => { });

    /// <summary>
    /// Bind to given IP address and port.
    /// </summary>
    public KestrelConfigurationLoader Endpoint(IPAddress address, int port, Action<ListenOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(address);

        return Endpoint(new IPEndPoint(address, port), configure);
    }

    /// <summary>
    /// Bind to given IP endpoint.
    /// </summary>
    public KestrelConfigurationLoader Endpoint(IPEndPoint endPoint) => Endpoint(endPoint, _ => { });

    /// <summary>
    /// Bind to given IP address and port.
    /// </summary>
    public KestrelConfigurationLoader Endpoint(IPEndPoint endPoint, Action<ListenOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(endPoint);
        ArgumentNullException.ThrowIfNull(configure);

        EndpointsToAdd.Add(() =>
        {
            Options.Listen(endPoint, configure);
        });

        return this;
    }

    /// <summary>
    /// Listens on ::1 and 127.0.0.1 with the given port. Requesting a dynamic port by specifying 0 is not supported
    /// for this type of endpoint.
    /// </summary>
    public KestrelConfigurationLoader LocalhostEndpoint(int port) => LocalhostEndpoint(port, options => { });

    /// <summary>
    /// Listens on ::1 and 127.0.0.1 with the given port. Requesting a dynamic port by specifying 0 is not supported
    /// for this type of endpoint.
    /// </summary>
    public KestrelConfigurationLoader LocalhostEndpoint(int port, Action<ListenOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        EndpointsToAdd.Add(() =>
        {
            Options.ListenLocalhost(port, configure);
        });

        return this;
    }

    /// <summary>
    /// Listens on all IPs using IPv6 [::], or IPv4 0.0.0.0 if IPv6 is not supported.
    /// </summary>
    public KestrelConfigurationLoader AnyIPEndpoint(int port) => AnyIPEndpoint(port, options => { });

    /// <summary>
    /// Listens on all IPs using IPv6 [::], or IPv4 0.0.0.0 if IPv6 is not supported.
    /// </summary>
    public KestrelConfigurationLoader AnyIPEndpoint(int port, Action<ListenOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        EndpointsToAdd.Add(() =>
        {
            Options.ListenAnyIP(port, configure);
        });

        return this;
    }

    /// <summary>
    /// Bind to given Unix domain socket path.
    /// </summary>
    public KestrelConfigurationLoader UnixSocketEndpoint(string socketPath) => UnixSocketEndpoint(socketPath, _ => { });

    /// <summary>
    /// Bind to given Unix domain socket path.
    /// </summary>
    public KestrelConfigurationLoader UnixSocketEndpoint(string socketPath, Action<ListenOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(socketPath);
        if (socketPath.Length == 0 || socketPath[0] != '/')
        {
            throw new ArgumentException(CoreStrings.UnixSocketPathMustBeAbsolute, nameof(socketPath));
        }
        ArgumentNullException.ThrowIfNull(configure);

        EndpointsToAdd.Add(() =>
        {
            Options.ListenUnixSocket(socketPath, configure);
        });

        return this;
    }

    /// <summary>
    /// Open a socket file descriptor.
    /// </summary>
    public KestrelConfigurationLoader HandleEndpoint(ulong handle) => HandleEndpoint(handle, _ => { });

    /// <summary>
    /// Open a socket file descriptor.
    /// </summary>
    public KestrelConfigurationLoader HandleEndpoint(ulong handle, Action<ListenOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        EndpointsToAdd.Add(() =>
        {
            Options.ListenHandle(handle, configure);
        });

        return this;
    }

    // Called from KestrelServerOptions.ApplyEndpointDefaults so it applies to even explicit Listen endpoints.
    // Does not require a call to Load.
    internal void ApplyEndpointDefaults(ListenOptions listenOptions)
    {
        var defaults = ConfigurationReader.EndpointDefaults;

        if (defaults.Protocols.HasValue)
        {
            listenOptions.Protocols = defaults.Protocols.Value;
        }
    }

    // Called from KestrelServerOptions.ApplyHttpsDefaults so it applies to even explicit Listen endpoints.
    // Does not require a call to Load.
    internal void ApplyHttpsDefaults(HttpsConnectionAdapterOptions httpsOptions)
    {
        var defaults = ConfigurationReader.EndpointDefaults;

        if (defaults.SslProtocols.HasValue)
        {
            httpsOptions.SslProtocols = defaults.SslProtocols.Value;
        }

        if (defaults.ClientCertificateMode.HasValue)
        {
            httpsOptions.ClientCertificateMode = defaults.ClientCertificateMode.Value;
        }
    }

    /// <summary>
    /// Loads the configuration.
    /// </summary>
    public void Load()
    {
        if (IsLoaded)
        {
            // The loader has already been run.
            return;
        }
        IsLoaded = true;

        Reload();

        foreach (var action in EndpointsToAdd)
        {
            action();
        }
    }

    // Adds endpoints from config to KestrelServerOptions.ConfigurationBackedListenOptions and configures some other options.
    // Any endpoints that were removed from the last time endpoints were loaded are returned.
    internal (List<ListenOptions>, List<ListenOptions>) Reload()
    {
        var endpointsToStop = Options.ConfigurationBackedListenOptions.ToList();
        var endpointsToStart = new List<ListenOptions>();
        var endpointsToReuse = new List<ListenOptions>();

        DefaultCertificateConfig = null;
        DefaultCertificate = null;

        ConfigurationReader = new ConfigurationReader(Configuration);

        if (_httpsConfigurationService.IsInitialized && _httpsConfigurationService.LoadDefaultCertificate(ConfigurationReader) is CertificateAndConfig certPair)
        {
            DefaultCertificate = certPair.Certificate;
            DefaultCertificateConfig = certPair.CertificateConfig;
        }

        foreach (var endpoint in ConfigurationReader.Endpoints)
        {
            var listenOptions = AddressBinder.ParseAddress(endpoint.Url, out var https);

            if (!https)
            {
                ConfigurationReader.ThrowIfContainsHttpsOnlyConfiguration(endpoint);
            }

            Options.ApplyEndpointDefaults(listenOptions);

            if (endpoint.Protocols.HasValue)
            {
                listenOptions.Protocols = endpoint.Protocols.Value;
            }
            else
            {
                // Ensure endpoint is reloaded if it used the default protocol and the protocol changed.
                // listenOptions.Protocols should already be set to this by ApplyEndpointDefaults.
                endpoint.Protocols = ConfigurationReader.EndpointDefaults.Protocols;
            }

            // Compare to UseHttps(httpsOptions => { })
            var httpsOptions = new HttpsConnectionAdapterOptions();

            if (https)
            {
                // Throws an appropriate exception if https configuration isn't enabled
                _httpsConfigurationService.ApplyHttpsConfiguration(httpsOptions, endpoint, Options, DefaultCertificateConfig, ConfigurationReader);
            }

            // Now that defaults have been loaded, we can compare to the currently bound endpoints to see if the config changed.
            // There's no reason to rerun an EndpointConfigurations callback if nothing changed.
            var matchingBoundEndpoints = new List<ListenOptions>();
            foreach (var o in endpointsToStop)
            {
                if (o.EndpointConfig == endpoint)
                {
                    matchingBoundEndpoints.Add(o);
                }
            }

            if (matchingBoundEndpoints.Count > 0)
            {
                endpointsToStop.RemoveAll(o => o.EndpointConfig == endpoint);
                endpointsToReuse.AddRange(matchingBoundEndpoints);
                continue;
            }

            if (EndpointConfigurations.TryGetValue(endpoint.Name, out var configureEndpoint))
            {
                var endpointConfig = new EndpointConfiguration(https, listenOptions, httpsOptions, endpoint.ConfigSection);
                configureEndpoint(endpointConfig);
            }

            // EndpointDefaults or configureEndpoint may have added an https adapter.
            if (https)
            {
                // This would throw if it were invoked without https configuration having been enabled,
                // but that won't happen because ApplyHttpsConfiguration would throw above under those
                // circumstances.
                _httpsConfigurationService.UseHttpsWithSni(listenOptions, httpsOptions, endpoint);
            }

            listenOptions.EndpointConfig = endpoint;

            endpointsToStart.Add(listenOptions);
        }

        // Update ConfigurationBackedListenOptions after everything else has been processed so that
        // it's left in a good state (i.e. its former state) if something above throws an exception.
        // Note that this isn't foolproof - we could run out of memory or something - but it covers
        // exceptions resulting from user misconfiguration (e.g. bad endpoint cert password).
        Options.ConfigurationBackedListenOptions.Clear();
        Options.ConfigurationBackedListenOptions.AddRange(endpointsToReuse);
        Options.ConfigurationBackedListenOptions.AddRange(endpointsToStart);

        return (endpointsToStop, endpointsToStart);
    }
}
