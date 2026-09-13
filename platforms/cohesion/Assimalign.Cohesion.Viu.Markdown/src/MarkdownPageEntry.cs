namespace Assimalign.Cohesion.Viu.Markdown;

/// <summary>Stores extracted page metadata without requiring Markdown parsing at runtime.</summary>
/// <param name="RoutePath">The normalized application route.</param>
/// <param name="AssetPath">The source asset path used by content sources.</param>
/// <param name="Title">The first-line heading or filename fallback.</param>
/// <param name="Description">The opening description paragraph.</param>
/// <param name="Status">The optional opening status blockquote.</param>
/// <param name="SourceRelativePath">The path relative to the physical content root.</param>
/// <param name="Order">The order within the containing section.</param>
public sealed record MarkdownPageEntry(
    string RoutePath,
    string AssetPath,
    string Title,
    string Description = "",
    string? Status = null,
    string SourceRelativePath = "",
    int Order = 0);
