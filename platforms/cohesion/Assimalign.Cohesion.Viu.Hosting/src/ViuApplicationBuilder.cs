using System;

using Assimalign.Cohesion;
using Assimalign.Cohesion.Configuration;
using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Cohesion.Hosting;
using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.State;

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
    private readonly ComponentFactory _defaultComponents = new();
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
        ApplicationOptions = new ApplicationOptions
        {
            Components = _defaultComponents,
        };
    }

    /// <summary>Gets the Cohesion service-provider builder owned by this composition.</summary>
    public ServiceProviderBuilder Services { get; }

    /// <summary>Gets the live Cohesion configuration manager owned by this composition.</summary>
    public ConfigurationManager Configuration { get; }

    /// <summary>Gets the Cohesion host environment exposed through the built provider and context.</summary>
    public HostEnvironment Environment { get; }

    /// <summary>Gets the mutable Viu composition options that <see cref="Build"/> snapshots.</summary>
    /// <remarks>
    /// Component contributions registered in <see cref="Services"/> join the default component
    /// factory; an explicitly assigned factory takes precedence over them. An unset state registry
    /// or directive resolver is resolved from the built provider, and unset warning and error
    /// handlers default to standard error.
    /// With no explicit factory or component contributions, generated component catalogs are the
    /// default. AddComponents() explicitly requests them alongside other contributions.
    /// </remarks>
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

    /// <summary>Selects a generated component definition as the application root.</summary>
    /// <typeparam name="TComponent">The component definition to describe without activating it.</typeparam>
    /// <returns>This builder.</returns>
    public ViuApplicationBuilder UseRootComponent<TComponent>()
        where TComponent : IComponentDefinition =>
        ConfigureApplication(options => options.RootComponent = ComponentNode.For<TComponent>());

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
    /// The builder was already built, no host factory was selected, or two component
    /// contributions register different components under the same reference.
    /// </exception>
    public ViuApplication Build()
    {
        InvalidOperationException.ThrowIf(_isBuilt, "The application has already been built.");
        _isBuilt = true;

        IViuApplicationHostFactory hostFactory = _hostFactory
            ?? throw new InvalidOperationException(
                "Configure a Viu application host before building the application.");

        ApplyDefaultDiagnostics();
        RegisterHostServices();
        RegisterApplicationServices();

        IServiceProvider serviceProvider = ((IServiceProviderBuilder)Services).Build();
        IApplication? runtimeApplication = null;
        try
        {
            ApplicationOptions frozenOptions = ApplicationOptionsSnapshot.Create(
                ApplicationOptions,
                serviceProvider);
            frozenOptions.Components = serviceProvider.GetRequiredService<IComponentFactory>();
            frozenOptions.State ??= serviceProvider.GetService<IStateStoreRegistry>();
            frozenOptions.Directives ??= serviceProvider.GetService<IDirectiveResolver>();
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

    private void ApplyDefaultDiagnostics()
    {
        ApplicationOptions.WarnHandler ??= ViuApplicationDiagnostics.WriteWarning;
        ApplicationOptions.ErrorHandler ??= ViuApplicationDiagnostics.WriteError;
    }

    private void RegisterHostServices()
    {
        Services.Add(ServiceDescriptor.Singleton<IConfiguration>(Configuration));
        Services.Add(ServiceDescriptor.Singleton<IConfigurationManager>(Configuration));
        Services.Add(ServiceDescriptor.Singleton(Configuration));
        Services.Add(ServiceDescriptor.Singleton<IHostEnvironment>(Environment));
    }

    private void RegisterApplicationServices()
    {
        // Captured here, after every ConfigureApplication call, so a later assignment on the
        // mutable options object cannot change the composed factory.
        ViuComponentComposition composition = ViuComponentComposition.GetOrAdd(Services);
        composition.ExplicitComponents = ApplicationOptions.Components;
        composition.DefaultComponents = _defaultComponents;
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
