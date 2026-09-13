using Assimalign.Viu.Components;

namespace Assimalign.Cohesion.Viu.Markdown;

/// <summary>Configures route names, an optional layout, and the not-found record before table creation.</summary>
/// <remarks>Not thread-safe; configure once at the application composition boundary.</remarks>
public sealed class MarkdownRouteOptions
{
    /// <summary>Creates route options with a not-found record and no parent layout.</summary>
    public MarkdownRouteOptions()
    {
    }

    /// <summary>Gets or sets the optional parent layout; its nested RouterView must use depth one.</summary>
    public ComponentReference? LayoutComponent { get; set; }

    /// <summary>Gets or sets the layout path, defaulting to root; absolute page paths retain their routes.</summary>
    public string LayoutPath { get; set; } = "/";

    /// <summary>Gets or sets the ordinal route-name prefix; page names append their normalized route.</summary>
    public string NamePrefix { get; set; } = "markdown:";

    /// <summary>Gets or sets whether unknown paths render the page view's not-found state.</summary>
    public bool IncludeNotFoundRoute { get; set; } = true;

    /// <summary>Gets or sets a custom catch-all component; null uses MarkdownPageView with the requested route.</summary>
    public ComponentReference? NotFoundComponent { get; set; }
}
