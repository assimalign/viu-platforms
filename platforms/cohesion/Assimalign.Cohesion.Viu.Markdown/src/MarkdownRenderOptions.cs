using System;

namespace Assimalign.Cohesion.Viu.Markdown;

/// <summary>Configures immutable Markdown output; renderers snapshot these values at construction.</summary>
/// <remarks>Configure options before constructing renderers. Mutating this options object is not thread-safe.</remarks>
public sealed class MarkdownRenderOptions
{
    /// <summary>Gets or sets the space-separated article classes; null omits the class attribute.</summary>
    public string? ArticleClass { get; set; } = "markdown-content";

    /// <summary>Gets or sets the heading identifier policy; the default creates deterministic slugs.</summary>
    public MarkdownHeadingIdentifierPolicy HeadingIdentifierPolicy { get; set; } = MarkdownHeadingIdentifierPolicy.Slug;

    /// <summary>
    /// Gets or sets a destination override called before catalog lookup; null results use built-in resolution.
    /// The callback receives the current page and the original Markdown destination.
    /// </summary>
    public Func<MarkdownPage, string, MarkdownLinkResolution?>? LinkResolver { get; set; }

    /// <summary>
    /// Gets or sets the HTML helper's route-to-href conversion; null uses the route unchanged.
    /// Hash-history hosts can supply a callback that prefixes routes with a hash character.
    /// The node renderer delegates href construction to RouterLink instead.
    /// </summary>
    public Func<string, string>? RouteHrefFactory { get; set; }
}
