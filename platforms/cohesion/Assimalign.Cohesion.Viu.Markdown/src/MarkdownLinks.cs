using System;
using System.Collections.Generic;

namespace Assimalign.Cohesion.Viu.Markdown;

/// <summary>Resolves Markdown destinations through ordinal catalog lookups without performing I/O.</summary>
public static class MarkdownLinks
{
    /// <summary>
    /// Resolves known Markdown assets to routes plus fragments, dropping asset query strings.
    /// Fragment-only, external, non-Markdown, and unknown destinations remain ordinary anchors.
    /// </summary>
    /// <param name="catalog">The catalog used to resolve asset identities.</param>
    /// <param name="currentPage">The source page whose asset directory anchors relative paths.</param>
    /// <param name="destination">The unencoded Markdown destination.</param>
    /// <returns>The immutable destination and its navigation owner.</returns>
    public static MarkdownLinkResolution Resolve(
        MarkdownContentCatalog catalog,
        MarkdownPage currentPage,
        string destination)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(currentPage);
        ArgumentNullException.ThrowIfNull(destination);
        if (destination.StartsWith('#')
            || destination.StartsWith("//", StringComparison.Ordinal)
            || Uri.TryCreate(destination, UriKind.Absolute, out Uri? absolute) && !absolute.IsFile)
        {
            return new(destination, false);
        }

        int fragmentIndex = destination.IndexOf('#', StringComparison.Ordinal);
        string fragment = fragmentIndex < 0 ? string.Empty : destination[fragmentIndex..];
        string path = fragmentIndex < 0 ? destination : destination[..fragmentIndex];
        int queryIndex = path.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
        {
            path = path[..queryIndex];
        }

        if (!path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return new(destination, false);
        }

        string assetPath = ResolveAssetPath(currentPage.AssetPath, path);
        return catalog.TryGetPageByAssetPath(assetPath, out MarkdownPage? page)
            ? new(page.RoutePath + fragment, true)
            : new(destination, false);
    }

    /// <summary>
    /// Resolves slash-separated relative segments against an asset's directory; parent segments
    /// stop at the root, absolute destinations start at the root, and empty or dot segments disappear.
    /// </summary>
    /// <param name="currentAssetPath">The current page's asset path.</param>
    /// <param name="destinationPath">The destination path without its query or fragment.</param>
    /// <returns>A slash-separated asset path without a leading slash.</returns>
    public static string ResolveAssetPath(string currentAssetPath, string destinationPath)
    {
        ArgumentNullException.ThrowIfNull(currentAssetPath);
        ArgumentNullException.ThrowIfNull(destinationPath);
        List<string> segments = [];
        if (!destinationPath.StartsWith('/'))
        {
            int separatorIndex = currentAssetPath.LastIndexOf('/');
            if (separatorIndex >= 0)
            {
                AddSegments(segments, currentAssetPath[..separatorIndex]);
            }
        }

        AddSegments(segments, destinationPath.TrimStart('/'));
        return string.Join('/', segments);
    }

    private static void AddSegments(List<string> segments, string path)
    {
        foreach (string segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count > 0)
                {
                    segments.RemoveAt(segments.Count - 1);
                }

                continue;
            }

            segments.Add(segment);
        }
    }
}
