using System;
using System.Collections.Generic;

using Assimalign.Cohesion.Configuration;
using Assimalign.Cohesion.Hosting;
using Assimalign.Viu;

namespace Assimalign.Cohesion.Viu.Hosting;

/// <summary>Describes the built Cohesion host surrounding one Viu runtime application.</summary>
public sealed class ViuApplicationContext : HostContext
{
    private readonly IHostService[] _hostedServices;

    internal ViuApplicationContext(
        IServiceProvider services,
        ConfigurationManager configuration,
        HostEnvironment environment,
        IReadOnlyList<IHostService> hostedServices,
        IApplication runtimeApplication)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(hostedServices);
        ArgumentNullException.ThrowIfNull(runtimeApplication);

        Services = services;
        Configuration = configuration;
        Environment = environment;
        _hostedServices = new IHostService[hostedServices.Count + 1];
        for (int index = 0; index < hostedServices.Count; index++)
        {
            _hostedServices[index] = hostedServices[index];
        }

        _hostedServices[^1] = new ViuApplicationHostService(runtimeApplication, this);
    }

    /// <summary>Gets the built Cohesion service provider supplied to Viu components.</summary>
    public IServiceProvider Services { get; }

    /// <summary>Gets the live configuration manager shared with application services and components.</summary>
    public ConfigurationManager Configuration { get; }

    /// <inheritdoc/>
    public override HostEnvironment Environment { get; }

    /// <inheritdoc/>
    public override IEnumerable<IHostService> HostedServices => _hostedServices;
}
