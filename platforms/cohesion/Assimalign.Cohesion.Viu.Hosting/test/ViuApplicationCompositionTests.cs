using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.State;

using Shouldly;

using Xunit;

namespace Assimalign.Cohesion.Viu.Hosting.Tests;

public sealed class ViuApplicationCompositionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AddComponents_ProviderCallbacks_UseBuiltServicesOnceInContributionOrder(bool assignFactory)
    {
        ComponentRegistration first = CreateRegistration("ProviderFirst");
        ComponentRegistration second = CreateRegistration("ProviderSecond");
        ComponentRegistration third = CreateRegistration("ProviderThird");
        ComponentRegistration assigned = CreateRegistration("ProviderFirst");
        CapturingHostFactory hostFactory = new();
        ViuApplicationBuilder builder = CreateConfiguredBuilder(hostFactory);
        List<string> calls = [];
        IServiceProvider? callbackProvider = null;

        builder.Services.AddComponents((serviceProvider, components) =>
        {
            callbackProvider = serviceProvider;
            components.Register(serviceProvider.GetRequiredService<ComponentRegistration>());
            calls.Add("first");
        });
        builder.Services.AddComponents(components =>
        {
            components.Register(second);
            calls.Add("second");
        });
        builder.Services.AddComponents((serviceProvider, components) =>
        {
            serviceProvider.ShouldBeSameAs(callbackProvider);
            components.Register(third);
            calls.Add("third");
        });
        // Later service registrations must be available without constructing an interim provider.
        builder.Services.AddSingleton(first);
        if (assignFactory)
        {
            ComponentFactory explicitFactory = new();
            explicitFactory.Register(assigned);
            builder.ConfigureApplication(options => options.Components = explicitFactory);
        }
        calls.ShouldBeEmpty();

        await using ViuApplication application = builder.Build();

        IComponentFactory resolved = application.Context.Services.GetRequiredService<IComponentFactory>();
        callbackProvider.ShouldNotBeNull().GetRequiredService<ComponentRegistration>().ShouldBeSameAs(first);
        callbackProvider.GetRequiredService<IComponentFactory>().ShouldBeSameAs(resolved);
        resolved.ShouldBeSameAs(hostFactory.Options!.Components);
        resolved.Resolve(first.Reference).ShouldBeSameAs(assignFactory ? assigned : first);
        resolved.Resolve(second.Reference).ShouldBeSameAs(second);
        resolved.Resolve(third.Reference).ShouldBeSameAs(third);
        application.Context.Services.GetRequiredService<IComponentFactory>().ShouldBeSameAs(resolved);
        calls.ShouldBe(["first", "second", "third"]);
    }

    [Fact]
    public void AddComponents_ProviderCallbackFailure_DisposesResolvedDependencies()
    {
        DisposableDependency dependency = new();
        InvalidOperationException failure = new("Component configuration failed.");
        CapturingHostFactory hostFactory = new();
        ViuApplicationBuilder builder = CreateConfiguredBuilder(hostFactory);
        builder.Services.AddSingleton<DisposableDependency>(_ => dependency);
        builder.Services.AddComponents((serviceProvider, _) =>
        {
            serviceProvider.GetRequiredService<DisposableDependency>().ShouldBeSameAs(dependency);
            throw failure;
        });

        Should.Throw<InvalidOperationException>(builder.Build).ShouldBeSameAs(failure);

        dependency.DisposeCount.ShouldBe(1);
        hostFactory.Options.ShouldBeNull();
    }

    [Fact]
    public void AddComponents_NullCallbacks_ThrowBeforeRegisteringContributions()
    {
        ViuApplicationBuilder builder = ViuApplication.CreateBuilder();
        int registrations = builder.Services.Container.Count;

        // The original null-literal call must continue to bind to the original overload.
        Should.Throw<ArgumentNullException>(() => builder.Services.AddComponents(null!));
        Should.Throw<ArgumentNullException>(() => builder.Services.AddComponents(
            (Action<IServiceProvider, ComponentFactory>)null!));

        builder.Services.Container.Count.ShouldBe(registrations);
    }

    private sealed class DisposableDependency : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }

    static ViuApplicationCompositionTests() => GeneratedComponentCatalogs.Register(
        "Cohesion.Hosting.Tests", static components => components.Register(GeneratedRoot.Registration));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GeneratedComponentsAreTheDefaultAndCanBeRequestedWithoutCallbacks(bool contribute)
    {
        CapturingHostFactory factory = new();
        ViuApplicationBuilder builder = CreateConfiguredBuilder(factory).UseRootComponent<GeneratedRoot>();
        if (contribute)
        {
            builder.Services.AddComponents().AddComponents();
        }

        await using ViuApplication application = builder.Build();

        factory.Options!.Components.Resolve(GeneratedRoot.Registration.Reference)
            .ShouldBeSameAs(GeneratedRoot.Registration);
        factory.Options.RootComponent.ShouldBeOfType<ComponentNode>().Component
            .ShouldBe(GeneratedRoot.Registration.Reference);
    }

    [Fact]
    public async Task ExplicitContributionsSuppressImplicitGeneratedComponents()
    {
        CapturingHostFactory factory = new();
        ViuApplicationBuilder builder = CreateConfiguredBuilder(factory);
        builder.Services.AddComponent(CreateRegistration("OnlyExplicit"));

        await using ViuApplication application = builder.Build();

        factory.Options!.Components.TryResolve(GeneratedRoot.Registration.Reference, out _).ShouldBeFalse();
    }

    [Fact]
    public async Task ExplicitOptionsPrecedeRequestedGeneratedComponents()
    {
        ComponentRegistration replacement = CreateRegistration("HostingGeneratedRoot");
        ComponentFactory assigned = new();
        assigned.Register(replacement);
        CapturingHostFactory factory = new();
        ViuApplicationBuilder builder = CreateConfiguredBuilder(factory);
        builder.Services.AddComponents();
        builder.ConfigureApplication(options => options.Components = assigned);

        await using ViuApplication application = builder.Build();

        factory.Options!.Components.Resolve(replacement.Reference).ShouldBeSameAs(replacement);
    }

    private sealed class GeneratedRoot : IComponentDefinition
    {
        public static ComponentRegistration Registration { get; } = CreateRegistration("HostingGeneratedRoot");
    }

    [Fact]
    public async Task ContributedComponentsComposeIntoTheRuntimeAndServiceFactory()
    {
        ComponentRegistration alpha = CreateRegistration("Alpha");
        ComponentRegistration beta = CreateRegistration("Beta");
        var factory = new CapturingHostFactory();
        ViuApplicationBuilder builder = ViuApplication.CreateBuilder();

        builder.Services
            .AddComponents(components => components.Register(alpha))
            .AddComponent(beta);
        builder.ConfigureApplication(options => options.RootComponent = new TextNode("root"));
        builder.ConfigureHost(factory);

        await using ViuApplication application = builder.Build();

        IComponentFactory components = factory.Options!.Components;
        components.Resolve(alpha.Reference).ShouldBeSameAs(alpha);
        components.Resolve(beta.Reference).ShouldBeSameAs(beta);
        components.ShouldBeSameAs(builder.ApplicationOptions.Components);
        application.Context.Services.GetRequiredService<IComponentFactory>()
            .ShouldBeSameAs(components);
        application.CreateApplicationOptionsSnapshot().Components.ShouldBeSameAs(components);
    }

    [Fact]
    public async Task ContributionsJoinRegistrationsMadeOnTheDefaultFactory()
    {
        ComponentRegistration registered = CreateRegistration("Registered");
        ComponentRegistration contributed = CreateRegistration("Contributed");
        var factory = new CapturingHostFactory();
        ViuApplicationBuilder builder = CreateConfiguredBuilder(factory);

        ((ComponentFactory)builder.ApplicationOptions.Components).Register(registered);
        builder.Services.AddComponent(contributed);

        await using ViuApplication application = builder.Build();

        IComponentFactory components = factory.Options!.Components;
        components.Resolve(registered.Reference).ShouldBeSameAs(registered);
        components.Resolve(contributed.Reference).ShouldBeSameAs(contributed);
    }

    [Fact]
    public async Task AssignedFactoryTakesPrecedenceRegardlessOfRegistrationOrder()
    {
        ComponentRegistration assignedCard = CreateRegistration("Card");
        ComponentRegistration contributedCard = CreateRegistration("Card");
        ComponentRegistration contributedBadge = CreateRegistration("Badge");
        ComponentFactory assigned = new();
        assigned.Register(assignedCard);
        var factory = new CapturingHostFactory();
        ViuApplicationBuilder builder = CreateConfiguredBuilder(factory);

        builder.Services.AddComponent(contributedCard).AddComponent(contributedBadge);
        builder.ConfigureApplication(options => options.Components = assigned);

        await using ViuApplication application = builder.Build();

        IComponentFactory components = factory.Options!.Components;
        components.Resolve(assignedCard.Reference).ShouldBeSameAs(assignedCard);
        components.Resolve(contributedBadge.Reference).ShouldBeSameAs(contributedBadge);
        components.TryResolve(ComponentReference.ForName("Missing"), out _).ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() =>
                components.Resolve(ComponentReference.ForName("Missing")))
            .Message.ShouldContain("Missing");
    }

    [Fact]
    public async Task AssignedFactoryIsUsedDirectlyWhenNothingIsContributed()
    {
        ComponentFactory assigned = new();
        var factory = new CapturingHostFactory();
        ViuApplicationBuilder builder = CreateConfiguredBuilder(factory);
        builder.ConfigureApplication(options => options.Components = assigned);

        await using ViuApplication application = builder.Build();

        factory.Options!.Components.ShouldBeSameAs(assigned);
    }

    [Fact]
    public async Task ContributingTheSameRegistrationTwiceRegistersItOnce()
    {
        ComponentRegistration registration = CreateRegistration("Repeated");
        var factory = new CapturingHostFactory();
        ViuApplicationBuilder builder = CreateConfiguredBuilder(factory);

        builder.Services.AddComponent(registration).AddComponent(registration);

        await using ViuApplication application = builder.Build();

        factory.Options!.Components.Resolve(registration.Reference).ShouldBeSameAs(registration);
    }

    [Fact]
    public void ConflictingContributionsFailTheBuild()
    {
        ViuApplicationBuilder builder = CreateConfiguredBuilder(new CapturingHostFactory());

        builder.Services
            .AddComponent(CreateRegistration("Conflict"))
            .AddComponent(CreateRegistration("Conflict"));

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(builder.Build);

        exception.Message.ShouldContain("already registered");
        exception.Message.ShouldContain("Conflict");
    }

    [Fact]
    public async Task ServiceRegisteredComponentFactoryReplacesComposition()
    {
        ComponentFactory replacement = new();
        var factory = new CapturingHostFactory();
        ViuApplicationBuilder builder = CreateConfiguredBuilder(factory);

        builder.Services.AddSingleton<IComponentFactory>(replacement);
        builder.Services.AddComponent(CreateRegistration("Ignored"));

        await using ViuApplication application = builder.Build();

        factory.Options!.Components.ShouldBeSameAs(replacement);
    }

    [Fact]
    public async Task UnsetStateAndDirectivesResolveFromServices()
    {
        var state = new StubStateStoreRegistry();
        var directives = new StubDirectiveResolver();
        var factory = new CapturingHostFactory();
        ViuApplicationBuilder builder = CreateConfiguredBuilder(factory);

        builder.Services.AddSingleton<IStateStoreRegistry>(state);
        builder.Services.AddSingleton<IDirectiveResolver>(directives);

        await using ViuApplication application = builder.Build();

        factory.Options!.State.ShouldBeSameAs(state);
        factory.Options.Directives.ShouldBeSameAs(directives);
    }

    [Fact]
    public async Task AssignedStateAndDirectivesTakePrecedenceOverServices()
    {
        var assignedState = new StubStateStoreRegistry();
        var assignedDirectives = new StubDirectiveResolver();
        var factory = new CapturingHostFactory();
        ViuApplicationBuilder builder = CreateConfiguredBuilder(factory);

        builder.Services.AddSingleton<IStateStoreRegistry>(new StubStateStoreRegistry());
        builder.Services.AddSingleton<IDirectiveResolver>(new StubDirectiveResolver());
        builder.ConfigureApplication(options =>
        {
            options.State = assignedState;
            options.Directives = assignedDirectives;
        });

        await using ViuApplication application = builder.Build();

        factory.Options!.State.ShouldBeSameAs(assignedState);
        factory.Options.Directives.ShouldBeSameAs(assignedDirectives);
    }

    [Fact]
    public async Task UnsetDiagnosticsReceiveDefaultsWhileAssignedHandlersArePreserved()
    {
        var defaultFactory = new CapturingHostFactory();
        await using (ViuApplication application = CreateConfiguredBuilder(defaultFactory).Build())
        {
            defaultFactory.Options!.WarnHandler.ShouldNotBeNull();
            defaultFactory.Options.ErrorHandler.ShouldNotBeNull();
        }

        Action<string> warnHandler = _ => { };
        Action<Exception, ComponentContext?, string> errorHandler = (_, _, _) => { };
        var assignedFactory = new CapturingHostFactory();
        ViuApplicationBuilder builder = CreateConfiguredBuilder(assignedFactory);
        builder.ConfigureApplication(options =>
        {
            options.WarnHandler = warnHandler;
            options.ErrorHandler = errorHandler;
        });

        await using ViuApplication assignedApplication = builder.Build();

        assignedFactory.Options!.WarnHandler.ShouldBeSameAs(warnHandler);
        assignedFactory.Options.ErrorHandler.ShouldBeSameAs(errorHandler);
    }

    private static ViuApplicationBuilder CreateConfiguredBuilder(IViuApplicationHostFactory factory)
    {
        ViuApplicationBuilder builder = ViuApplication.CreateBuilder();
        builder.ConfigureApplication(options => options.RootComponent = new TextNode("root"));
        builder.ConfigureHost(factory);
        return builder;
    }

    private static ComponentRegistration CreateRegistration(string name) =>
        ComponentRegistration.Define(name, new ComponentContract(), static _ => static _ => new TextNode("component"));

    private sealed class CapturingHostFactory : IViuApplicationHostFactory
    {
        public ApplicationOptions? Options { get; private set; }

        public IApplication CreateApplication(ApplicationOptions applicationOptions)
        {
            Options = applicationOptions;
            return new CapturedApplication(applicationOptions);
        }
    }

    private sealed class CapturedApplication : IApplication
    {
        private readonly ApplicationLifetime _lifetime;

        public CapturedApplication(ApplicationOptions options)
        {
            var context = new ApplicationContext(options);
            _lifetime = new ApplicationLifetime(context);
            Context = context;
        }

        public IApplicationContext Context { get; }

        public IApplication Use(ApplicationMiddleware middleware) => this;

        public ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            _lifetime.StartExecution();
            _lifetime.SignalRunning();
            return ValueTask.CompletedTask;
        }

        public ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            _lifetime.CompleteStopping();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _lifetime.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubStateStoreRegistry : IStateStoreRegistry
    {
        public int Count => 0;

        public bool IsDisposed { get; private set; }

        public TStore GetOrCreate<TStore>(StateStoreDefinition<TStore> definition)
            where TStore : class => throw new NotSupportedException();

        public bool Remove<TStore>(StateStoreDefinition<TStore> definition)
            where TStore : class => false;

        public void Dispose() => IsDisposed = true;
    }

    private sealed class StubDirectiveResolver : IDirectiveResolver
    {
        public IDirective? Resolve(Type directiveType) => null;
    }
}
