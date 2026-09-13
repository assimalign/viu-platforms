using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Router;
using Assimalign.Viu.Testing;

using Shouldly;

using Xunit;

using ViuRouter = Assimalign.Viu.Router.Router;

namespace Assimalign.Cohesion.Viu.Markdown.Tests;

public sealed class MarkdownPageViewTests
{
    [Fact]
    public async Task MountLoadsNodesAndInternalLinkNavigatesThroughRouter()
    {
        ControlledSource source = new();
        using IRouterHistory history = RouterHistory.CreateMemory();
        using ViuRouter router = new(history, MarkdownRoutes.Create(Catalog));
        await router.PushAsync("/first");
        using ComponentWrapper wrapper = MountRouter(router, source);
        wrapper.Html().ShouldContain("Loading documentation");

        source.Complete("docs/first.md", "# First\n\n[Next](second.md#details)");
        await wrapper.FlushAsync();
        wrapper.Get("h1").Attribute("id").ShouldBe("first");
        wrapper.Get("a").Attribute("href").ShouldBe("/second#details");
        await wrapper.Get("a").TriggerAsync("click");
        router.CurrentRoute.Value.Path.ShouldBe("/second#details");
        source.Complete("docs/second.md", "# Second\n\n## Details");
        await wrapper.FlushAsync();
        wrapper.Get("h2").Attribute("id").ShouldBe("details");
        source.ReadCount.ShouldBe(2);
    }

    [Fact]
    public async Task ReusedMountCancelsSupersededReadAndIgnoresLateSuccess()
    {
        ControlledSource source = new();
        RouteParent parent = new();
        using ComponentWrapper wrapper = ComponentTest.Mount(parent, Options(source));
        CancellationToken previous = source.Token("docs/first.md");
        parent.Route.Value = "/second";
        await wrapper.FlushAsync();
        previous.IsCancellationRequested.ShouldBeTrue();
        source.Complete("docs/second.md", "# Second");
        await wrapper.FlushAsync();
        source.Complete("docs/first.md", "# Obsolete");
        await wrapper.FlushAsync();
        wrapper.Get("h1").Text().ShouldBe("Second");
        source.ReadCount.ShouldBe(2);
    }

    [Fact]
    public async Task ReusedMountIgnoresLateFailureAndShowsCurrentFetchFailure()
    {
        ControlledSource source = new();
        RouteParent parent = new();
        using ComponentWrapper wrapper = ComponentTest.Mount(parent, Options(source));
        parent.Route.Value = "/second";
        await wrapper.FlushAsync();
        source.Fail("docs/first.md", "obsolete failure");
        source.Fail("docs/second.md", "network unavailable");
        await wrapper.FlushAsync();
        wrapper.Get("p").Attribute("role").ShouldBe("alert");
        wrapper.Text().ShouldContain("docs/second.md: network unavailable");
        wrapper.Text().ShouldNotContain("obsolete failure");
    }

    [Fact]
    public async Task UnknownRouteCancelsLoadWithoutReadingAnotherAsset()
    {
        ControlledSource source = new();
        RouteParent parent = new();
        using ComponentWrapper wrapper = ComponentTest.Mount(parent, Options(source));
        parent.Route.Value = "/missing";
        await wrapper.FlushAsync();
        source.Token("docs/first.md").IsCancellationRequested.ShouldBeTrue();
        source.Complete("docs/first.md", "# Stale");
        await wrapper.FlushAsync();
        wrapper.Text().ShouldContain("No Markdown page is registered for /missing.");
        source.ReadCount.ShouldBe(1);
    }

    [Fact]
    public async Task UnmountCancelsTheOutstandingSourceRead()
    {
        ControlledSource source = new();
        using ComponentWrapper wrapper = ComponentTest.Mount(new RouteParent(), Options(source));
        CancellationToken token = source.Token("docs/first.md");
        wrapper.Unmount();
        token.IsCancellationRequested.ShouldBeTrue();
        source.Complete("docs/first.md", "# Finished after unmount");
        await wrapper.FlushAsync();
    }

    [Fact]
    public async Task ServerPrefetchAwaitsSourceBeforeReturningRenderedTree()
    {
        ControlledSource source = new();
        ComponentMountOptions options = Options(source);
        ComponentHost host = new(new ComponentRuntimeOptions(options.Components!, new NoWatchScheduler(), options.Services));
        ValueTask<IComponentRenderScope> operation = host.RenderAsync(new ComponentRenderRequest(
            new ComponentNode(MarkdownPageView.Registration.Reference, new ComponentInvocation(
                new Dictionary<string, object?> { ["route"] = "/first" }))));
        operation.IsCompleted.ShouldBeFalse();
        source.Complete("docs/first.md", "# Prefetched");
        await using IComponentRenderScope scope = await operation;
        ElementNode article = scope.Tree.ShouldBeOfType<ElementNode>();
        article.Name.LocalName.ShouldBe("article");
        source.ReadCount.ShouldBe(1);
    }

    [Fact]
    public async Task NavigationUsesCatalogOrderAndTracksTheActiveLink()
    {
        using IRouterHistory history = RouterHistory.CreateMemory();
        using ViuRouter router = new(history, MarkdownRoutes.Create(Catalog));
        await router.PushAsync("/second#heading");
        using ComponentWrapper wrapper = ComponentTest.Mount(MarkdownNavigation.Registration,
            Options(new ControlledSource(), router));
        IReadOnlyList<ElementWrapper> links = wrapper.FindAll("a");
        links.Count.ShouldBe(2);
        links[0].Text().ShouldBe("First");
        links[1].Attribute("class").ShouldBeOfType<string>().Split(' ').ShouldContain("active");
    }

    private static readonly MarkdownContentCatalog Catalog = new(
        [new MarkdownSection("Guide", "", [new MarkdownPageEntry("/first", "docs/first.md", "First"),
            new MarkdownPageEntry("/second", "docs/second.md", "Second")])]);

    private static ComponentWrapper MountRouter(ViuRouter router, ControlledSource source)
        => ComponentTest.Mount(RouterView.Registration, Options(source, router));

    private static ComponentMountOptions Options(ControlledSource source, ViuRouter? router = null)
    {
        ComponentFactory components = new();
        MarkdownComponents.Register(components);
        return new ComponentMountOptions
        {
            Components = components,
            Services = new TestServices(source, router),
        };
    }

    private sealed class TestServices(IMarkdownContentSource source, ViuRouter? router) : IServiceProvider
    {
        private readonly MarkdownVirtualNodeRenderer renderer = new(Catalog);

        public object? GetService(Type serviceType)
            => serviceType == typeof(MarkdownContentCatalog) ? Catalog
                : serviceType == typeof(IMarkdownContentSource) ? source
                : serviceType == typeof(MarkdownVirtualNodeRenderer) ? renderer
                : serviceType == typeof(ViuRouter) ? router : null;
    }

    private sealed class RouteParent : IComponent
    {
        internal ShallowReference<string> Route { get; } = Reactive.ShallowReference("/first");

        public ComponentRenderer Setup(ComponentContext context) => _ => new ComponentNode(
            MarkdownPageView.Registration.Reference,
            new ComponentInvocation(new Dictionary<string, object?> { ["route"] = Route.Value }));
    }

    private sealed class ControlledSource : IMarkdownContentSource
    {
        private readonly Dictionary<string, TaskCompletionSource<string>> requests = new(StringComparer.Ordinal);
        private readonly Dictionary<string, CancellationToken> tokens = new(StringComparer.Ordinal);

        internal int ReadCount { get; private set; }

        public Task<string> ReadAsync(string assetPath, CancellationToken cancellationToken = default)
        {
            ReadCount++;
            tokens[assetPath] = cancellationToken;
            TaskCompletionSource<string> request = new();
            requests.Add(assetPath, request);
            return request.Task;
        }

        internal CancellationToken Token(string path) => tokens[path];
        internal void Complete(string path, string text) => requests[path].SetResult(text);
        internal void Fail(string path, string message) => requests[path].SetException(new InvalidOperationException(message));
    }

    private sealed class NoWatchScheduler : IReactiveWatchScheduler
    {
        public void Schedule(WatchJob job) => throw new InvalidOperationException("Server prefetch must not schedule a watcher.");
    }
}
