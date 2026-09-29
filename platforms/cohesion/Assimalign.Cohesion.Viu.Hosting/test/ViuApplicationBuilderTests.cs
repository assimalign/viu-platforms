using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Cohesion.Configuration;
using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Cohesion.Hosting;
using Assimalign.Viu;
using Assimalign.Viu.Components;

using Shouldly;

using Xunit;

namespace Assimalign.Cohesion.Viu.Hosting.Tests;

public sealed class ViuApplicationBuilderTests
{
    [Fact]
    public async Task BuildSnapshotsCompositionAndFlowsServicesAndConfiguration()
    {
        var root = new TextNode("first");
        var service = new ApplicationService("from services");
        var factory = new RecordingHostFactory();
        ViuApplicationBuilder builder = ViuApplication.CreateBuilder();

        builder.Services.Add(ServiceDescriptor.Singleton(service));
        builder.Configuration["application:message"] = "from configuration";
        builder.ConfigureApplication(options => options.RootComponent = root);
        builder.ConfigureHost(factory);

        ViuApplication application = builder.Build();
        builder.ApplicationOptions.RootComponent = new TextNode("changed");

        factory.Options.ShouldNotBeNull();
        factory.Options.ShouldNotBeSameAs(builder.ApplicationOptions);
        application.ApplicationContext.RootComponent.ShouldBeSameAs(root);
        application.Context.Services.GetRequiredService<ApplicationService>()
            .ShouldBeSameAs(service);
        application.Context.Configuration["application:message"].ShouldBe("from configuration");
        application.Context.Services.GetRequiredService<IConfiguration>()
            .ShouldBeSameAs(builder.Configuration);
        application.Context.Services.GetRequiredService<IConfigurationManager>()
            .ShouldBeSameAs(builder.Configuration);
        application.Context.Services.GetRequiredService<ConfigurationManager>()
            .ShouldBeSameAs(builder.Configuration);
        application.Context.Services.GetRequiredService<IHostEnvironment>()
            .ShouldBeSameAs(builder.Environment);

        ApplicationOptions firstSnapshot = application.CreateApplicationOptionsSnapshot();
        ApplicationOptions secondSnapshot = application.CreateApplicationOptionsSnapshot();
        firstSnapshot.ShouldNotBeSameAs(secondSnapshot);
        firstSnapshot.RootComponent.ShouldBeSameAs(root);
        firstSnapshot.Services.ShouldBeSameAs(application.Context.Services);

        await ((IAsyncDisposable)application).DisposeAsync();
    }

    [Fact]
    public async Task RunStartsUserServicesBeforeViuAndStopsInReverseOrder()
    {
        List<string> events = [];
        var service = new RecordingHostService(events);
        var factory = new RecordingHostFactory(events);
        ViuApplicationBuilder builder = CreateConfiguredBuilder(factory);
        builder.Services.Add(ServiceDescriptor.Singleton<IHostService>(service));

        ViuApplication application = builder.Build();
        Task run = application.RunAsync();
        await factory.Application!.Started.WaitAsync(TimeSpan.FromSeconds(5));

        application.Context.State.ShouldBe(HostState.Started);
        application.ApplicationContext.IsRunning.ShouldBeTrue();
        events.ShouldBe(["service:start", "viu:start"]);

        factory.Application.RequestStopping();
        await run.WaitAsync(TimeSpan.FromSeconds(5));

        application.Context.State.ShouldBe(HostState.Stopped);
        application.ApplicationContext.IsRunning.ShouldBeFalse();
        events.ShouldBe([
            "service:start",
            "viu:start",
            "viu:stop",
            "service:stop",
        ]);

        await ((IAsyncDisposable)application).DisposeAsync();
        events.ShouldBe([
            "service:start",
            "viu:start",
            "viu:stop",
            "service:stop",
            "viu:dispose",
        ]);
    }

    [Fact]
    public async Task DisposeOwnsRuntimeProviderAndConfiguration()
    {
        List<string> events = [];
        var disposableService = new DisposableApplicationService(events);
        var factory = new RecordingHostFactory(events);
        ViuApplicationBuilder builder = CreateConfiguredBuilder(factory);
        builder.Services.Add(ServiceDescriptor.Singleton<DisposableApplicationService>(
            _ => disposableService));

        ViuApplication application = builder.Build();
        application.Context.Services.GetRequiredService<DisposableApplicationService>()
            .ShouldBeSameAs(disposableService);

        await ((IAsyncDisposable)application).DisposeAsync();

        factory.Application!.IsDisposed.ShouldBeTrue();
        disposableService.IsDisposed.ShouldBeTrue();
        events.ShouldBe(["viu:dispose", "service:dispose"]);
        Should.Throw<ObjectDisposedException>(() =>
            _ = application.Context.Configuration.Providers);
    }

    [Fact]
    public async Task BuildActivatesServicesRegisteredByImplementationType()
    {
        var dependency = new ApplicationService("dependency");
        ViuApplicationBuilder builder = CreateConfiguredBuilder(new RecordingHostFactory());
        builder.Services.Add(ServiceDescriptor.Singleton(dependency));
        builder.Services.AddSingleton<IActivatedService, ActivatedService>();

        ViuApplication application = builder.Build();

        IActivatedService activated =
            application.Context.Services.GetRequiredService<IActivatedService>();
        activated.ShouldBeOfType<ActivatedService>().Dependency.ShouldBeSameAs(dependency);
        application.Context.Services.GetRequiredService<IActivatedService>()
            .ShouldBeSameAs(activated);

        await ((IAsyncDisposable)application).DisposeAsync();
    }

    [Fact]
    public void BuildRequiresAnExplicitHostAndCanRunOnlyOnce()
    {
        ViuApplicationBuilder unconfigured = ViuApplication.CreateBuilder();
        unconfigured.ConfigureApplication(options =>
            options.RootComponent = new TextNode("root"));

        Should.Throw<InvalidOperationException>(unconfigured.Build)
            .Message.ShouldContain("Configure a Viu application host");

        ViuApplicationBuilder configured =
            CreateConfiguredBuilder(new RecordingHostFactory());
        ViuApplication application = configured.Build();

        Should.Throw<InvalidOperationException>(configured.Build)
            .Message.ShouldContain("already been built");

        ((IDisposable)application).Dispose();
    }

    [Fact]
    public async Task CohesionHostCannotRestartTheSingleUseViuRuntime()
    {
        var factory = new RecordingHostFactory();
        ViuApplication application = CreateConfiguredBuilder(factory).Build();

        await application.StartAsync();
        await application.StopAsync();

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => application.StartAsync());

        exception.Message.ShouldContain("can start only once");
        factory.Application!.StartCount.ShouldBe(1);

        await ((IAsyncDisposable)application).DisposeAsync();
    }

    private static ViuApplicationBuilder CreateConfiguredBuilder(
        IViuApplicationHostFactory factory)
    {
        ViuApplicationBuilder builder = ViuApplication.CreateBuilder();
        builder.ConfigureApplication(options =>
            options.RootComponent = new TextNode("root"));
        builder.ConfigureHost(factory);
        return builder;
    }

    private sealed record ApplicationService(string Value);

    private interface IActivatedService
    {
    }

    private sealed class ActivatedService : IActivatedService
    {
        public ActivatedService(ApplicationService dependency)
        {
            Dependency = dependency;
        }

        public ApplicationService Dependency { get; }
    }

    private sealed class DisposableApplicationService : IDisposable
    {
        private readonly List<string> _events;

        public DisposableApplicationService(List<string> events)
        {
            _events = events;
        }

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
            _events.Add("service:dispose");
        }
    }

    private sealed class RecordingHostFactory : IViuApplicationHostFactory
    {
        private readonly List<string> _events;

        public RecordingHostFactory(List<string>? events = null)
        {
            _events = events ?? [];
        }

        public ApplicationOptions? Options { get; private set; }

        public RecordingApplication? Application { get; private set; }

        public IApplication CreateApplication(ApplicationOptions applicationOptions)
        {
            Options = applicationOptions;
            Application = new RecordingApplication(applicationOptions, _events);
            return Application;
        }
    }

    private sealed class RecordingApplication : IApplication
    {
        private readonly List<string> _events;
        private readonly ApplicationLifetime _lifetime;
        private readonly TaskCompletionSource<object?> _startedSource = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public RecordingApplication(
            ApplicationOptions options,
            List<string> events)
        {
            _events = events;
            var context = new ApplicationContext(options);
            _lifetime = new ApplicationLifetime(context);
            Context = context;
        }

        public IApplicationContext Context { get; }

        public Task Started => _startedSource.Task;

        public bool IsDisposed { get; private set; }

        public int StartCount { get; private set; }

        public IApplication Use(ApplicationMiddleware middleware)
        {
            ArgumentNullException.ThrowIfNull(middleware);
            return this;
        }

        public ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartCount++;
            _events.Add("viu:start");
            _lifetime.StartExecution();
            _lifetime.SignalRunning();
            _startedSource.TrySetResult(null);
            return ValueTask.CompletedTask;
        }

        public ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_lifetime.State is ApplicationState.Starting
                or ApplicationState.Running
                or ApplicationState.Stopping)
            {
                _events.Add("viu:stop");
                _lifetime.CompleteStopping();
            }

            return ValueTask.CompletedTask;
        }

        public void RequestStopping() => _lifetime.RequestStopping();

        public ValueTask DisposeAsync()
        {
            if (IsDisposed)
            {
                return ValueTask.CompletedTask;
            }

            IsDisposed = true;
            _events.Add("viu:dispose");
            _lifetime.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingHostService : IHostService
    {
        private readonly List<string> _events;

        public RecordingHostService(List<string> events)
        {
            _events = events;
        }

        public ServiceId Id { get; } = ServiceId.New();

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _events.Add("service:start");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _events.Add("service:stop");
            return Task.CompletedTask;
        }
    }
}
