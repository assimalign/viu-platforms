using System;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Cohesion.Viu.Hosting;
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
        ComponentFactory components = new();
        MarkdownContentCatalog catalog = new([]);
        StubSource source = new();

        builder.AddMarkdownContent(options =>
        {
            options.Catalog = catalog;
            options.Source = source;
            options.Components = components;
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
        builder.ApplicationOptions.Components.ShouldBeSameAs(components);
        components.Resolve(ComponentReference.ForName(nameof(MarkdownPageView)))
            .Contract.ShouldBeSameAs(MarkdownPageView.Registration.Contract);
    }

    [Fact]
    public void SourceFactoryRunsLazilyOnceWithTheHostsProvider()
    {
        ViuApplicationBuilder builder = ViuApplication.CreateBuilder();
        MarkdownContentCatalog catalog = new([]);
        StubSource source = new();
        int calls = 0;
        builder.ConfigureApplication(options => options.Components = new ComponentFactory());
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
        builder.ConfigureApplication(options => options.Components = new ComponentFactory());
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
}
