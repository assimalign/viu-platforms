using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Cohesion.Viu.Markdown.Internal;

namespace Assimalign.Cohesion.Viu.Markdown;

/// <summary>Builds the immutable catalog from Markdown text, explicit metadata, or a listing source.</summary>
/// <remarks>This mutable builder belongs to one application event loop and does not synchronize concurrent calls.</remarks>
public sealed class MarkdownContentCatalogBuilder
{
    private readonly string _contentRootPrefix;
    private readonly string _routeBase;
    private readonly List<ExtractedPage> _pages = [];
    private readonly List<MarkdownContentDiagnostic> _diagnostics = [];
    private readonly List<MarkdownContentDiagnostic> _extractionDiagnostics = [];

    /// <summary>Sets the asset prefix and route base used for all subsequently added Markdown files.</summary>
    public MarkdownContentCatalogBuilder(string contentRootPrefix = "docs/", string routeBase = "/")
    {
        _contentRootPrefix = MarkdownContentCatalog.NormalizeAssetPath(contentRootPrefix).TrimEnd('/');
        _routeBase = MarkdownContentCatalog.NormalizeRoute(routeBase);
    }

    /// <summary>Gets a read-only view of extraction and most recent build diagnostics.</summary>
    public IReadOnlyList<MarkdownContentDiagnostic> Diagnostics => _diagnostics.AsReadOnly();

    /// <summary>Extracts one file relative to the content root using the shared line-based algorithm.</summary>
    public MarkdownContentCatalogBuilder Add(string sourceRelativePath, string markdown)
    {
        ArgumentNullException.ThrowIfNull(sourceRelativePath);
        ArgumentNullException.ThrowIfNull(markdown);
        _pages.Add(ContentExtraction.Extract(sourceRelativePath, markdown, _contentRootPrefix, _routeBase,
            diagnostic => _extractionDiagnostics.Add(ConvertDiagnostic(diagnostic))));
        return this;
    }

    /// <summary>Adds explicit metadata without reading or parsing Markdown; filenames determine sections.</summary>
    public MarkdownContentCatalogBuilder Add(MarkdownPageEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        string relativePath = entry.SourceRelativePath.Length == 0 ? RemoveAssetPrefix(entry.AssetPath) : entry.SourceRelativePath;
        _pages.Add(new ExtractedPage
        {
            RoutePath = MarkdownContentCatalog.NormalizeRoute(entry.RoutePath),
            AssetPath = MarkdownContentCatalog.NormalizeAssetPath(entry.AssetPath),
            SourceRelativePath = MarkdownContentCatalog.NormalizeAssetPath(relativePath),
            Title = entry.Title,
            Description = entry.Description,
            Status = entry.Status,
            Order = entry.Order,
        });
        return this;
    }

    /// <summary>Constructs an independent immutable catalog and throws if route or source duplicates exist.</summary>
    public MarkdownContentCatalog Build()
    {
        _diagnostics.Clear();
        _diagnostics.AddRange(_extractionDiagnostics);
        ExtractedSection root = ContentExtraction.Arrange(_pages,
            diagnostic => _diagnostics.Add(ConvertDiagnostic(diagnostic)));
        if (_diagnostics.Any(diagnostic => diagnostic.IsError))
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine,
                _diagnostics.Where(diagnostic => diagnostic.IsError).Select(diagnostic => diagnostic.Message)));
        }
        return new MarkdownContentCatalog([CreateSection(root)]);
    }

    /// <summary>Reads the source's ordinal asset listing, adds its files, and builds a catalog with cancellation.</summary>
    public async Task<MarkdownContentCatalog> BuildAsync(IMarkdownContentSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        IReadOnlyList<string> assets = await source.ListAsync(cancellationToken).ConfigureAwait(false);
        foreach (string asset in assets.OrderBy(path => path, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Add(RemoveAssetPrefix(asset), await source.ReadAsync(asset, cancellationToken).ConfigureAwait(false));
        }
        return Build();
    }

    private string RemoveAssetPrefix(string assetPath)
    {
        string normalized = MarkdownContentCatalog.NormalizeAssetPath(assetPath);
        return _contentRootPrefix.Length > 0 && normalized.StartsWith(_contentRootPrefix + "/", StringComparison.Ordinal)
            ? normalized[(_contentRootPrefix.Length + 1)..] : normalized;
    }

    private static MarkdownSection CreateSection(ExtractedSection section)
        => new(section.Title, section.Path,
            section.Pages.Select(page => new MarkdownPageEntry(page.RoutePath, page.AssetPath, page.Title,
                page.Description, page.Status, page.SourceRelativePath, page.Order)),
            section.Sections.Select(CreateSection), section.Order);

    private static MarkdownContentDiagnostic ConvertDiagnostic(ExtractionDiagnostic diagnostic)
        => new(diagnostic.Identifier, diagnostic.Path, diagnostic.Message, diagnostic.IsError);
}
