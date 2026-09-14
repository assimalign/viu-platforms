using System;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Cohesion.Content.Markdown;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Cohesion.Viu.Markdown;

/// <summary>Loads a catalog route and renders Markdown nodes with loading and error states.</summary>
/// <remarks>Not thread-safe; each mount runs on one Viu event loop and cancels superseded reads.</remarks>
public sealed class MarkdownPageView : IComponent
{
    private static readonly ComponentContract Contract = new(
        displayName: nameof(MarkdownPageView),
        parameters: [new ComponentParameter("route", defaultFactory: static () => "/")]);

    /// <summary>Creates one page-view instance; the runtime supplies its services during setup.</summary>
    public MarkdownPageView()
    {
    }

    /// <summary>Gets the static contract and explicit per-mount activator without attribute discovery.</summary>
    public static ComponentRegistration Registration { get; } = new(
        ComponentReference.ForType(typeof(MarkdownPageView)), Contract,
        static _ => new MarkdownPageView());

    /// <summary>Gets or sets the catalog route, defaulting to the root; bindings refresh it on each render.</summary>
    [Parameter("route")]
    public string Route { get; set; } = "/";

    /// <summary>Resolves services and registers cancellable mount, update, and awaited server-prefetch loads.</summary>
    /// <param name="context">The runtime-owned context whose services contain the catalog, source, and renderer.</param>
    /// <returns>A reactive render function that emits one loading, error, or article subtree.</returns>
    public ComponentRenderer Setup(ComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        MarkdownContentCatalog catalog = Resolve<MarkdownContentCatalog>(context);
        IMarkdownContentSource source = Resolve<IMarkdownContentSource>(context);
        MarkdownVirtualNodeRenderer renderer = Resolve<MarkdownVirtualNodeRenderer>(context);
        ShallowReference<PageState> state = Reactive.ShallowReference(new PageState(true, null, null));
        CancellationTokenSource? pending = null;
        string? activeRoute = null;
        int requestVersion = 0;
        string defaultRoute = Route;

        void ReadRoute()
        {
            Route = context.Bindings.Parameters.TryGetValue("route", out object? value)
                && value is string route ? route : defaultRoute;
        }

        async Task LoadAsync(CancellationToken cancellationToken)
        {
            ReadRoute();
            string route = MarkdownContentCatalog.NormalizeRoute(Route);
            if (string.Equals(route, activeRoute, StringComparison.Ordinal))
            {
                return;
            }

            activeRoute = route;
            int version = ++requestVersion;
            pending?.Cancel();
            pending = null;
            if (!catalog.TryGetPageByRoute(route, out MarkdownPage? page))
            {
                state.Value = new PageState(false, $"No Markdown page is registered for {route}.", null);
                return;
            }

            using CancellationTokenSource request = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, context.Lifecycle.CancellationToken);
            pending = request;
            state.Value = new PageState(true, null, null);
            try
            {
                string markdown = await source.ReadAsync(page.AssetPath, request.Token);
                if (version == requestVersion && !request.IsCancellationRequested)
                {
                    state.Value = new PageState(false, null, renderer.Render(MarkdownText.Parse(markdown), page));
                }
            }
            catch (OperationCanceledException) when (request.IsCancellationRequested)
            {
                if (version == requestVersion)
                {
                    activeRoute = null;
                }
            }
            catch (Exception exception)
            {
                if (version == requestVersion && !request.IsCancellationRequested)
                {
                    state.Value = new PageState(false, $"{page.AssetPath}: {exception.Message}", null);
                }
            }
            finally
            {
                if (version == requestVersion)
                {
                    pending = null;
                }
            }
        }

        ReadRoute();
        context.Lifecycle.OnMounted(LoadAsync);
        context.Lifecycle.OnUpdated(LoadAsync);
        context.Lifecycle.OnServerPrefetch(LoadAsync);
        context.Lifecycle.OnBeforeUnmount(() =>
        {
            ++requestVersion;
            pending?.Cancel();
            pending = null;
        });

        return _ =>
        {
            ReadRoute();
            PageState current = state.Value;
            if (current.Loading)
            {
                return Message("markdown-loading", "status", "Loading documentation…");
            }
            return current.Error is not null
                ? Message("markdown-error", "alert", current.Error)
                : current.Tree;
        };
    }

    private static TService Resolve<TService>(ComponentContext context) where TService : class
        => context.Services?.GetService(typeof(TService)) as TService
            ?? throw new InvalidOperationException($"The Markdown service {typeof(TService).Name} is unavailable.");

    private static ElementNode Message(string className, string role, string text)
        => new(new QualifiedName("p"),
            [ElementBinding.Attribute(new QualifiedName("class"), className),
             ElementBinding.Attribute(new QualifiedName("role"), role)],
            [new TextNode(text)]);

    private sealed record PageState(bool Loading, string? Error, VirtualNode? Tree);
}
