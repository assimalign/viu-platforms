using System;

using Assimalign.Cohesion;
using Assimalign.Cohesion.Configuration;
using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Cohesion.Hosting;
using Assimalign.Viu;

namespace Assimalign.Cohesion.Viu.Hosting;

/// <summary>
/// Composes a Viu application with Cohesion configuration, dependency injection, and hosting.
/// </summary>
/// <remarks>
/// The builder is host-neutral. A platform package selects the concrete Viu runtime through
/// <see cref="ConfigureHost(IViuApplicationHostFactory)"/>. Each builder can produce one
/// application.
/// </remarks>
public sealed class ViuApplicationBuilder : IApplicationBuilder, IHostBuilder
{
    private IViuApplicationHostFactory? _hostFactory;
    private bool _isBuilt;

    /// <summary>Initializes an empty Viu application composition.</summary>
    public ViuApplicationBuilder()
    {
        Services = new ServiceProviderBuilder();
        Configuration = new ConfigurationManager(new ConfigurationOptions
        {
            SetStrategy = ConfigurationSetStrategy.Distributed,
        });
        Configuration.AddProvider(new ViuApplicationConfigurationProvider());
        Environment = new HostEnvironment
        {
            Name = AppEnvironment.GetEnvironmentName(),
        };
        ApplicationOptions = new ApplicationOptions();
    }

    /// <summary>Gets the Cohesion service-provider builder owned by this composition.</summary>
    public ServiceProviderBuilder Services { get; }

    /// <summary>Gets the live Cohesion configuration manager owned by this composition.</summary>
    public ConfigurationManager Configuration { get; }

    /// <summary>Gets the Cohesion host environment exposed through the built provider and context.</summary>
    public HostEnvironment Environment { get; }

    /// <summary>Gets the mutable Viu composition options that <see cref="Build"/> snapshots.</summary>
    public ApplicationOptions ApplicationOptions { get; }

    /// <summary>Applies Viu composition settings.</summary>
    /// <param name="configure">The action that mutates <see cref="ApplicationOptions"/>.</param>
    /// <returns>This builder.</returns>
    public ViuApplicationBuilder ConfigureApplication(Action<ApplicationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        InvalidOperationException.ThrowIf(_isBuilt, "The application has already been built.");

        configure(ApplicationOptions);
        return this;
    }

    /// <summary>Selects the host-specific Viu runtime factory.</summary>
    /// <param name="hostFactory">The explicit, reflection-free host factory.</param>
    /// <returns>This builder.</returns>
    public ViuApplicationBuilder ConfigureHost(IViuApplicationHostFactory hostFactory)
    {
        ArgumentNullException.ThrowIfNull(hostFactory);
        InvalidOperationException.ThrowIf(_isBuilt, "The application has already been built.");

        _hostFactory = hostFactory;
        return this;
    }

    /// <summary>Builds the configured Cohesion-hosted Viu application.</summary>
    /// <returns>The single application built by this builder.</returns>
    /// <exception cref="InvalidOperationException">
    /// The builder was already built, no host factory was selected, or a service registration
    /// requests reflection-based type activation.
    /// </exception>
    public ViuApplication Build()
    {
        InvalidOperationException.ThrowIf(_isBuilt, "The application has already been built.");
        _isBuilt = true;

        IViuApplicationHostFactory hostFactory = _hostFactory
            ?? throw new InvalidOperationException(
                "Configure a Viu application host before building the application.");

        RegisterHostServices();
        RejectTypeActivation();

        IServiceProvider serviceProvider = ((IServiceProviderBuilder)Services).Build();
        IApplication? runtimeApplication = null;
        try
        {
            ApplicationOptions frozenOptions = ApplicationOptionsSnapshot.Create(
                ApplicationOptions,
                serviceProvider);
            runtimeApplication = hostFactory.CreateApplication(
                ApplicationOptionsSnapshot.Create(frozenOptions));
            ArgumentNullException.ThrowIfNull(runtimeApplication);

            IHostService[] hostedServices = [.. serviceProvider.GetServices<IHostService>()];
            var context = new ViuApplicationContext(
                serviceProvider,
                Configuration,
                Environment,
                hostedServices,
                runtimeApplication);

            return new ViuApplication(
                context,
                runtimeApplication,
                frozenOptions,
                serviceProvider,
                Configuration);
        }
        catch
        {
            if (runtimeApplication is not null)
            {
                runtimeApplication.DisposeAsync()
                    .AsTask()
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
            }

            DisposeServiceProvider(serviceProvider);
            Configuration.Dispose();
            throw;
        }
    }

    IApplicationBuilder IApplicationBuilder.ConfigureApplication(
        Action<ApplicationOptions> configure) => ConfigureApplication(configure);

    IApplication IApplicationBuilder.Build() => Build();

    IHost IHostBuilder.Build() => Build();

    private void RegisterHostServices()
    {
        Services.Add(ServiceDescriptor.Singleton<IConfiguration>(Configuration));
        Services.Add(ServiceDescriptor.Singleton<IConfigurationManager>(Configuration));
        Services.Add(ServiceDescriptor.Singleton(Configuration));
        Services.Add(ServiceDescriptor.Singleton<IHostEnvironment>(Environment));
    }

    private void RejectTypeActivation()
    {
        foreach (ServiceDescriptor descriptor in Services.Services)
        {
            if (descriptor.ImplementationType is not null)
            {
                throw new InvalidOperationException(
                    $"The Viu hosting model does not permit type activation for service " +
                    $"'{descriptor.ServiceType}'. Register an instance or explicit factory instead.");
            }
        }
    }

    private static void DisposeServiceProvider(IServiceProvider serviceProvider)
    {
        if (serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
            return;
        }

        if (serviceProvider is IAsyncDisposable asyncDisposable)
        {
            asyncDisposable.DisposeAsync()
                .AsTask()
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }
    }
}
