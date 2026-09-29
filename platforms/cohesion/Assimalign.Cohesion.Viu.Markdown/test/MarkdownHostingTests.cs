using System;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Cohesion.Viu.Hosting;
using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Router;

using Shouldly;

using Xunit;

namespace Assimalign.Cohesion.Viu.Markdown.Tests;

public sealed class MarkdownHostingTests
{
    [Fact]
    public void RegistersOnlyInstancesAndFactoriesAndResolvesRuntimeDependencies()
    {
        ViuApplicationBuilder builder = ViuApplication.CreateBuilder();
        MarkdownContentCatalog catalog = new([]);
        StubSource source = new();

        builder.AddMarkdownContent(options =>
        {
            options.Catalog = catalog;
            options.Source = source;
        }).ShouldBeSameAs(builder);

        foreach (ServiceDescriptor descriptor in builder.Services.Container)
        {
            descriptor.ImplementationType.ShouldBeNull();
            (descriptor.ImplementationInstance is not null || descriptor.ImplementationFactory is not null)
                .ShouldBeTrue();
        }
        IServiceProvider services = ((IServiceProviderBuilder)builder.Services).Build();
        using IDisposable lifetime = (IDisposable)services;
        services.GetRequiredService<MarkdownContentCatalog>().ShouldBeSameAs(catalog);
        services.GetRequiredService<IMarkdownContentSource>().ShouldBeSameAs(source);
        services.GetRequiredService<MarkdownVirtualNodeRenderer>().ShouldNotBeNull();
        services.GetRequiredService<MarkdownHtmlRenderer>().ShouldNotBeNull();
    }

    [Fact]
    public async Task ComponentsReachTheApplicationFactoryRegardlessOfConfigurationOrder()
    {
        ComponentFactory assignedLater = new();
        var factory = new CapturingHostFactory();
        ViuApplicationBuilder builder = ViuApplication.CreateBuilder();
        builder.AddMarkdownContent(options =>
        {
            options.Catalog = new MarkdownContentCatalog([]);
            options.Source = new StubSource();
        });

        // Assigning a factory after registration used to drop the Markdown components.
        builder.ConfigureApplication(options =>
        {
            options.RootComponent = new TextNode("root");
            options.Components = assignedLater;
        });
        builder.ConfigureHost(factory);

        await using ViuApplication application = builder.Build();

        IComponentFactory components = factory.Options!.Components;
        components.Resolve(ComponentReference.ForName(nameof(MarkdownPageView)))
            .Contract.ShouldBeSameAs(MarkdownPageView.Registration.Contract);
        components.Resolve(MarkdownNavigation.Registration.Reference)
            .ShouldBeSameAs(MarkdownNavigation.Registration);
        components.Resolve(ComponentReference.ForName(nameof(RouterView)))
            .Contract.ShouldBeSameAs(RouterView.Registration.Contract);
    }

    [Fact]
    public void SourceFactoryRunsLazilyOnceWithTheHostsProvider()
    {
        ViuApplicationBuilder builder = ViuApplication.CreateBuilder();
        MarkdownContentCatalog catalog = new([]);
        StubSource source = new();
        int calls = 0;
        builder.AddMarkdownContent(options =>
        {
            options.Catalog = catalog;
            options.SourceFactory = services =>
            {
                services.GetRequiredService<MarkdownContentCatalog>().ShouldBeSameAs(catalog);
                calls++;
                return source;
            };
        });
        calls.ShouldBe(0);

        IServiceProvider services = ((IServiceProviderBuilder)builder.Services).Build();
        using IDisposable lifetime = (IDisposable)services;
        services.GetRequiredService<IMarkdownContentSource>().ShouldBeSameAs(source);
        services.GetRequiredService<IMarkdownContentSource>().ShouldBeSameAs(source);
        calls.ShouldBe(1);
    }

    [Fact]
    public void RejectsMissingCatalogAndAmbiguousSourcesBeforeRegistering()
    {
        ViuApplicationBuilder builder = ViuApplication.CreateBuilder();
        Should.Throw<InvalidOperationException>(() => builder.AddMarkdownContent(_ => { }))
            .Message.ShouldContain("catalog");
        Should.Throw<InvalidOperationException>(() => builder.AddMarkdownContent(options =>
            options.Catalog = new MarkdownContentCatalog([])))
            .Message.ShouldContain("exactly one");
        Should.Throw<InvalidOperationException>(() => builder.AddMarkdownContent(options =>
        {
            options.Catalog = new MarkdownContentCatalog([]);
            options.Source = new StubSource();
            options.SourceFactory = _ => new StubSource();
        })).Message.ShouldContain("exactly one");
    }

    [Fact]
    public void ComponentRegistrationIsIdempotentAndPreservesCallerOverrides()
    {
        ComponentFactory components = new();
        ComponentRegistration custom = new(ComponentReference.ForName(nameof(MarkdownPageView)),
            MarkdownPageView.Registration.Contract, static _ => new MarkdownPageView());
        components.Register(custom);

        MarkdownComponents.Register(components);
        MarkdownComponents.Register(components);

        components.Resolve(custom.Reference).ShouldBeSameAs(custom);
        foreach (ComponentRegistration registration in new[]
        {
            MarkdownPageView.Registration, MarkdownNavigation.Registration,
            RouterLink.Registration, RouterView.Registration,
        })
        {
            components.Resolve(registration.Reference).ShouldBeSameAs(registration);
        }
        components.Resolve(ComponentReference.ForName(nameof(RouterLink)))
            .Contract.ShouldBeSameAs(RouterLink.Registration.Contract);
        components.Resolve(ComponentReference.ForName(nameof(RouterView)))
            .Contract.ShouldBeSameAs(RouterView.Registration.Contract);
        components.Resolve(ComponentReference.ForName(nameof(MarkdownNavigation)))
            .Contract.ShouldBeSameAs(MarkdownNavigation.Registration.Contract);
    }

    private sealed class StubSource : IMarkdownContentSource
    {
        public Task<string> ReadAsync(string assetPath, CancellationToken cancellationToken = default)
            => Task.FromResult("# Documentation");
    }

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

        public ValueTask StartAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask StopAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            _lifetime.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
