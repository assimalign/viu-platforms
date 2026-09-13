using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;
using Assimalign.Viu.Router;

namespace Assimalign.Cohesion.Viu.Markdown;

/// <summary>Creates eagerly declared, named route records without activating any components.</summary>
public static class MarkdownRoutes
{
    /// <summary>Creates one exact page record in catalog order, optionally nested beneath a layout.</summary>
    /// <param name="catalog">The immutable page catalog.</param>
    /// <param name="options">Composition options, or null for defaults.</param>
    /// <returns>A read-only route table; page arguments are fixed and the catch-all forwards the requested path.</returns>
    public static IReadOnlyList<RouteRecord> Create(MarkdownContentCatalog catalog, MarkdownRouteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        options ??= new MarkdownRouteOptions();
        ArgumentNullException.ThrowIfNull(options.NamePrefix);
        List<RouteRecord> records = [];
        string layoutPath = MarkdownContentCatalog.NormalizeRoute(options.LayoutPath);
        foreach (MarkdownPage page in catalog.Pages)
        {
            string path = options.LayoutComponent is not null && page.RoutePath == layoutPath
                ? string.Empty : PagePattern(page.RoutePath);
            records.Add(new RouteRecord(path, options.NamePrefix + page.RoutePath,
                component: new ComponentNode(MarkdownPageView.Registration.Reference),
                argumentsResolver: RouteComponentArguments.FromValues(("route", page.RoutePath))));
        }
        if (options.IncludeNotFoundRoute)
        {
            records.Add(new RouteRecord("/:pathMatch(.*)*", options.NamePrefix + "not-found",
                component: new ComponentNode(options.NotFoundComponent ?? MarkdownPageView.Registration.Reference),
                argumentsResolver: static route => new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["route"] = route.Path,
                }));
        }
        if (options.LayoutComponent is not null)
        {
            return Array.AsReadOnly(new[]
            {
                new RouteRecord(PagePattern(layoutPath), options.NamePrefix + "layout",
                    children: records.AsReadOnly(), component: new ComponentNode(options.LayoutComponent)),
            });
        }
        return records.AsReadOnly();
    }

    private static string EscapePath(string path) => path.Replace(":", "\\:", StringComparison.Ordinal);

    // Router beta.11 passes location suffixes to its path-only matcher. A bounded optional
    // parameter preserves exact page matching and named paths while accepting heading links.
    private static string PagePattern(string path) => EscapePath(path) + ":markdownSuffix([?#].*)?";
}
