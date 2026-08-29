using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Cohesion.Configuration;
using Assimalign.Cohesion.Hosting;
using Assimalign.Viu;

namespace Assimalign.Cohesion.Viu.Hosting;

/// <summary>
/// Hosts one platform-specific Viu application inside the Cohesion host lifecycle.
/// </summary>
/// <remarks>
/// User host services start before the Viu runtime and stop after it. The application owns the
/// built service provider, configuration manager, and runtime application.
/// </remarks>
public sealed class ViuApplication : Host<ViuApplicationContext>, IApplication
{
    private readonly IApplication _runtimeApplication;
    private readonly ApplicationOptions _applicationOptions;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConfigurationManager _configuration;
    private bool _executionClaimed;
    private bool _resourcesDisposed;

    internal ViuApplication(
        ViuApplicationContext context,
        IApplication runtimeApplication,
        ApplicationOptions applicationOptions,
        IServiceProvider serviceProvider,
        ConfigurationManager configuration)
        : base(new ViuApplicationHostOptions
        {
            Environment = context.Environment.Name,
        })
    {
        Context = context;
        _runtimeApplication = runtimeApplication;
        _applicationOptions = applicationOptions;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    /// <summary>Creates a host-neutral application builder.</summary>
    /// <returns>A new Viu application builder.</returns>
    public static ViuApplicationBuilder CreateBuilder() => new();

    /// <inheritdoc/>
    public override ViuApplicationContext Context { get; }

    /// <summary>Gets the frozen context of the underlying Viu runtime.</summary>
    public IApplicationContext ApplicationContext => _runtimeApplication.Context;

    /// <summary>
    /// Creates a new options object over the frozen application composition references.
    /// </summary>
    /// <returns>
    /// A shallow snapshot suitable for a second designed Viu host, such as a server renderer.
    /// </returns>
    public ApplicationOptions CreateApplicationOptionsSnapshot() =>
        ApplicationOptionsSnapshot.Create(_applicationOptions);

    /// <summary>Appends middleware around the underlying Viu runtime lifetime.</summary>
    /// <param name="middleware">The middleware to append before execution begins.</param>
    /// <returns>This application.</returns>
    public ViuApplication Use(ApplicationMiddleware middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        _runtimeApplication.Use(middleware);
        return this;
    }

    /// <summary>Starts Cohesion host services and then the Viu runtime.</summary>
    /// <param name="cancellationToken">Cancels startup and requests graceful shutdown.</param>
    /// <returns>A task representing host startup.</returns>
    public Task StartAsync(CancellationToken cancellationToken = default) =>
        ((IHost)this).StartAsync(cancellationToken);

    /// <summary>Stops the Viu runtime and then Cohesion host services.</summary>
    /// <param name="cancellationToken">Cancels this shutdown wait.</param>
    /// <returns>A task representing host shutdown.</returns>
    public Task StopAsync(CancellationToken cancellationToken = default) =>
        ((IHost)this).StopAsync(cancellationToken);

    /// <summary>
    /// Starts the application, waits for cancellation or runtime shutdown, and then stops it.
    /// </summary>
    /// <param name="cancellationToken">Requests graceful shutdown of the persistent host.</param>
    /// <returns>A task representing the complete host lifetime.</returns>
    public new Task RunAsync(CancellationToken cancellationToken = default) =>
        base.RunAsync(cancellationToken);

    IApplicationContext IApplication.Context => _runtimeApplication.Context;

    IApplication IApplication.Use(ApplicationMiddleware middleware) => Use(middleware);

    ValueTask IApplication.StartAsync(CancellationToken cancellationToken) =>
        new(StartAsync(cancellationToken));

    ValueTask IApplication.StopAsync(CancellationToken cancellationToken) =>
        new(StopAsync(cancellationToken));

    /// <inheritdoc/>
    protected override Task OnStartingAsync(CancellationToken cancellationToken = default)
    {
        InvalidOperationException.ThrowIf(
            _executionClaimed,
            "A Viu application lifetime can start only once.");
        _executionClaimed = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    protected override async ValueTask DisposeAsync(bool disposing)
    {
        if (!disposing || _resourcesDisposed)
        {
            await base.DisposeAsync(disposing).ConfigureAwait(false);
            return;
        }

        _resourcesDisposed = true;
        List<Exception> exceptions = [];

        try
        {
            await base.DisposeAsync(disposing).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            exceptions.Add(exception);
            try
            {
                await base.DisposeAsync(disposing).ConfigureAwait(false);
            }
            catch (Exception repeatedException)
            {
                exceptions.Add(repeatedException);
            }
        }

        await DisposeRuntimeAsync(exceptions).ConfigureAwait(false);
        await DisposeServiceProviderAsync(exceptions).ConfigureAwait(false);
        await DisposeConfigurationAsync(exceptions).ConfigureAwait(false);

        if (exceptions.Count == 1)
        {
            ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
        }

        if (exceptions.Count > 1)
        {
            throw new AggregateException(
                "One or more Viu application resources failed to dispose.",
                exceptions);
        }
    }

    private async ValueTask DisposeRuntimeAsync(List<Exception> exceptions)
    {
        try
        {
            await _runtimeApplication.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            exceptions.Add(exception);
        }
    }

    private async ValueTask DisposeServiceProviderAsync(List<Exception> exceptions)
    {
        try
        {
            if (_serviceProvider is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch (Exception exception)
        {
            exceptions.Add(exception);
        }
    }

    private async ValueTask DisposeConfigurationAsync(List<Exception> exceptions)
    {
        try
        {
            await _configuration.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            exceptions.Add(exception);
        }
    }
}
