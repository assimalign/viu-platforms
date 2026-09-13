namespace Assimalign.Cohesion.Viu.Markdown;

/// <summary>Describes an immutable Markdown page and its owning navigation section.</summary>
public sealed class MarkdownPage
{
    /// <summary>Creates immutable metadata for a page; catalog construction attaches its section.</summary>
    public MarkdownPage(string routePath, string assetPath, string title, string description = "",
        string? status = null, int order = 0, string sourceRelativePath = "")
        : this(new MarkdownPageEntry(routePath, assetPath, title, description, status, sourceRelativePath, order), null)
    {
    }

    internal MarkdownPage(MarkdownPageEntry entry, MarkdownSection? section)
    {
        RoutePath = MarkdownContentCatalog.NormalizeRoute(entry.RoutePath);
        AssetPath = MarkdownContentCatalog.NormalizeAssetPath(entry.AssetPath);
        Title = entry.Title;
        Description = entry.Description;
        Status = entry.Status;
        Section = section;
        Order = entry.Order;
        SourceRelativePath = entry.SourceRelativePath;
    }

    /// <summary>Gets the normalized, ordinal application route for this page.</summary>
    public string RoutePath { get; }
    /// <summary>Gets the normalized asset path passed unchanged to the content source.</summary>
    public string AssetPath { get; }
    /// <summary>Gets the title extracted from the first line or the filename fallback.</summary>
    public string Title { get; }
    /// <summary>Gets the opening description paragraph, with wrapped lines joined by spaces.</summary>
    public string Description { get; }
    /// <summary>Gets the opening status blockquote without its status marker, when present.</summary>
    public string? Status { get; }
    /// <summary>Gets the owning immutable section, or null for a standalone page.</summary>
    public MarkdownSection? Section { get; }
    /// <summary>Gets the zero-based order within the owning section.</summary>
    public int Order { get; }
    /// <summary>Gets the source path relative to the physical content root.</summary>
    public string SourceRelativePath { get; }
}
