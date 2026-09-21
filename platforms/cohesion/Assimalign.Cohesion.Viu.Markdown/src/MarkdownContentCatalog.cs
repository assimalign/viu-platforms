using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Assimalign.Cohesion.Viu.Markdown;

/// <summary>Indexes immutable sections and pages with ordinal route and asset comparisons.</summary>
public sealed class MarkdownContentCatalog
{
    private readonly Dictionary<string, MarkdownPage> _pagesByRoute = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MarkdownPage> _pagesByAssetPath = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MarkdownPage> _pagesBySourcePath = new(StringComparer.Ordinal);

    /// <summary>Copies the section tree and rejects duplicate normalized routes or assets.</summary>
    public MarkdownContentCatalog(IEnumerable<MarkdownSection> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        Sections = Array.AsReadOnly(sections.ToArray());
        List<MarkdownPage> pages = [];
        foreach (MarkdownSection section in Sections)
        {
            AddSection(section, pages);
        }
        Pages = pages.AsReadOnly();
    }

    /// <summary>Gets root sections in navigation order.</summary>
    public IReadOnlyList<MarkdownSection> Sections { get; }
    /// <summary>Gets all pages in navigation order, interleaving direct pages and nested sections by their order.</summary>
    public IReadOnlyList<MarkdownPage> Pages { get; }

    /// <summary>Finds a page using normalized ordinal route equality without changing case.</summary>
    public bool TryGetPageByRoute(string routePath, [NotNullWhen(true)] out MarkdownPage? page)
        => _pagesByRoute.TryGetValue(NormalizeRoute(routePath), out page);

    /// <summary>Finds a page using normalized ordinal asset equality without changing case.</summary>
    public bool TryGetPageByAssetPath(string assetPath, [NotNullWhen(true)] out MarkdownPage? page)
        => _pagesByAssetPath.TryGetValue(NormalizeAssetPath(assetPath), out page);

    /// <summary>
    /// Finds a page by its path relative to the physical content root, using normalized ordinal
    /// equality. Pages created without a source path are not indexed here.
    /// </summary>
    /// <remarks>
    /// Asset paths are delivery names and may be hashed; the source path is the stable identity
    /// that relative Markdown links between pages refer to.
    /// </remarks>
    public bool TryGetPageBySourcePath(string sourceRelativePath, [NotNullWhen(true)] out MarkdownPage? page)
        => _pagesBySourcePath.TryGetValue(NormalizeAssetPath(sourceRelativePath), out page);

    /// <summary>Converts separators, trims whitespace and edge slashes, and guarantees a leading slash.</summary>
    public static string NormalizeRoute(string routePath)
    {
        ArgumentNullException.ThrowIfNull(routePath);
        string normalized = routePath.Replace('\\', '/').Trim();
        return normalized.Length == 0 || normalized == "/" ? "/" : "/" + normalized.Trim('/');
    }

    /// <summary>Converts separators and removes leading slashes, preserving case and other characters.</summary>
    public static string NormalizeAssetPath(string assetPath)
    {
        ArgumentNullException.ThrowIfNull(assetPath);
        return assetPath.Replace('\\', '/').TrimStart('/');
    }

    private void AddSection(MarkdownSection section, List<MarkdownPage> pages)
    {
        IEnumerable<(int Order, MarkdownPage? Page, MarkdownSection? Section)> navigation = section.Pages
            .Select(page => (page.Order, (MarkdownPage?)page, (MarkdownSection?)null))
            .Concat(section.Sections.Select(child => (child.Order, (MarkdownPage?)null, (MarkdownSection?)child)))
            .OrderBy(item => item.Order);
        foreach ((int _, MarkdownPage? page, MarkdownSection? child) in navigation)
        {
            if (page is not null)
            {
                _pagesByRoute.Add(page.RoutePath, page);
                _pagesByAssetPath.Add(page.AssetPath, page);
                if (page.SourceRelativePath.Length > 0)
                {
                    _pagesBySourcePath.TryAdd(NormalizeAssetPath(page.SourceRelativePath), page);
                }
                pages.Add(page);
            }
            else if (child is not null)
            {
                AddSection(child, pages);
            }
        }
    }
}
