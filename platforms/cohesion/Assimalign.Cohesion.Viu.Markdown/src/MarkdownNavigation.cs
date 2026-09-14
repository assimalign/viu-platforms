using System;
using System.Collections.Generic;
using System.Linq;

using Assimalign.Viu.Components;
using Assimalign.Viu.Router;

namespace Assimalign.Cohesion.Viu.Markdown;

/// <summary>Renders catalog sections as nested navigation lists of router-aware page links.</summary>
/// <remarks>Not thread-safe; each instance belongs to one mounted Viu event loop.</remarks>
public sealed class MarkdownNavigation : IComponent
{
    private static readonly ComponentContract Contract = new(displayName: nameof(MarkdownNavigation),
        parameters: [new ComponentParameter("activeClass", defaultFactory: static () => "active")]);

    /// <summary>Creates a navigation component whose catalog is resolved at setup.</summary>
    public MarkdownNavigation()
    {
    }

    /// <summary>Gets the reflection-free registration and declared input contract.</summary>
    public static ComponentRegistration Registration { get; } = new(
        ComponentReference.ForType(typeof(MarkdownNavigation)), Contract,
        static _ => new MarkdownNavigation());

    /// <summary>Gets or sets the exact-active CSS class forwarded to each RouterLink.</summary>
    [Parameter("activeClass")]
    public string ActiveClass { get; set; } = "active";

    /// <summary>Resolves the catalog once and renders immutable lists in its navigation order.</summary>
    /// <param name="context">The runtime-owned context with a catalog and router in its services.</param>
    /// <returns>The render closure whose child RouterLinks track the active route.</returns>
    public ComponentRenderer Setup(ComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        MarkdownContentCatalog catalog = context.Services?.GetService(typeof(MarkdownContentCatalog)) as MarkdownContentCatalog
            ?? throw new InvalidOperationException("The Markdown catalog is unavailable.");
        global::Assimalign.Viu.Router.Router? router = context.Services?.GetService(
            typeof(global::Assimalign.Viu.Router.Router)) as global::Assimalign.Viu.Router.Router;
        return _ =>
        {
            ActiveClass = context.Bindings.Parameters.TryGetValue("activeClass", out object? value)
                && value is string name ? name : "active";
            List<VirtualNode> sections = [];
            string currentRoute = MarkdownContentCatalog.NormalizeRoute(router?.CurrentRoute.Value.Path ?? string.Empty);
            foreach (MarkdownSection section in catalog.Sections)
            {
                sections.Add(RenderSection(section, currentRoute));
            }
            return new ElementNode(new QualifiedName("nav"),
                [ElementBinding.Attribute(new QualifiedName("aria-label"), "Documentation")],
                [new ElementNode(new QualifiedName("ul"), children: sections)]);
        };
    }

    private ElementNode RenderSection(MarkdownSection section, string currentRoute)
    {
        List<(int Order, VirtualNode Node)> entries = [];
        foreach (MarkdownPage page in section.Pages)
        {
            ComponentNode link = new(RouterLink.Registration.Reference, new ComponentInvocation(
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["to"] = page.RoutePath,
                    ["exactActiveClass"] = ActiveClass,
                    ["class"] = string.Equals(page.RoutePath, currentRoute, StringComparison.Ordinal) ? ActiveClass : null,
                },
                new Dictionary<string, ComponentSlot>(StringComparer.Ordinal)
                {
                    ["default"] = _ => new TextNode(page.Title),
                }));
            entries.Add((page.Order, new ElementNode(new QualifiedName("li"), children: [link])));
        }
        foreach (MarkdownSection child in section.Sections)
        {
            entries.Add((child.Order, RenderSection(child, currentRoute)));
        }
        return new ElementNode(new QualifiedName("li"), children:
        [new ElementNode(new QualifiedName("span"), children: [new TextNode(section.Title)]),
         new ElementNode(new QualifiedName("ul"), children: entries.OrderBy(entry => entry.Order).Select(entry => entry.Node))]);
    }
}
