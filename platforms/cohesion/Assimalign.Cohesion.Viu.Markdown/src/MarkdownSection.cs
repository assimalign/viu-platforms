using System;
using System.Collections.Generic;
using System.Linq;

namespace Assimalign.Cohesion.Viu.Markdown;

/// <summary>Groups immutable pages and nested sections in navigation order.</summary>
public sealed class MarkdownSection
{
    /// <summary>Copies ordered entries and children and creates pages referencing this section.</summary>
    public MarkdownSection(string title, string sourceRelativePath,
        IEnumerable<MarkdownPageEntry> pages, IEnumerable<MarkdownSection>? sections = null, int order = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(pages);
        Title = title;
        SourceRelativePath = sourceRelativePath;
        Order = order;
        Pages = Array.AsReadOnly(pages.Select(entry => new MarkdownPage(entry, this)).ToArray());
        Sections = Array.AsReadOnly(sections?.ToArray() ?? []);
    }

    /// <summary>Gets the index heading or humanized folder name.</summary>
    public string Title { get; }
    /// <summary>Gets the folder path relative to the physical content root; the root is empty.</summary>
    public string SourceRelativePath { get; }
    /// <summary>Gets this section's position among its parent's pages and sections; unspecified sections follow pages.</summary>
    public int Order { get; }
    /// <summary>Gets the copied, immutable list of direct pages in navigation order.</summary>
    public IReadOnlyList<MarkdownPage> Pages { get; }
    /// <summary>Gets the copied, immutable list of child sections in navigation order.</summary>
    public IReadOnlyList<MarkdownSection> Sections { get; }
}
