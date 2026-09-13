namespace Assimalign.Cohesion.Viu.Markdown;

/// <summary>Specifies whether headings receive deterministic identifiers.</summary>
public enum MarkdownHeadingIdentifierPolicy
{
    /// <summary>Creates lowercase letter-and-digit slugs with duplicate numeric suffixes.</summary>
    Slug,

    /// <summary>Leaves headings without generated identifiers.</summary>
    None,
}
