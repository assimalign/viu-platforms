using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using Assimalign.Cohesion.Content.Markdown;
using Assimalign.Cohesion.Viu.Markdown.Internal;

namespace Assimalign.Cohesion.Viu.Markdown;

/// <summary>
/// Renders a Markdown HTML fragment through MarkdownText.ToHtml, then adds heading identifiers
/// and catalog-aware href values without changing the input document.
/// </summary>
/// <remarks>
/// This helper produces ordinary HTML anchors. Hosts that need Viu navigation should use
/// MarkdownVirtualNodeRenderer. Each call owns its traversal state; callback thread safety is borrowed.
/// </remarks>
public sealed class MarkdownHtmlRenderer
{
    private readonly MarkdownContentCatalog _catalog;
    private readonly MarkdownHeadingIdentifierPolicy _headingIdentifierPolicy;
    private readonly Func<MarkdownPage, string, MarkdownLinkResolution?>? _linkResolver;
    private readonly Func<string, string>? _routeHrefFactory;

    /// <summary>Initializes an HTML renderer that snapshots options and borrows the catalog.</summary>
    /// <param name="catalog">The immutable content catalog used to resolve links.</param>
    /// <param name="options">The rendering policy, or null for default identifiers and ordinary route hrefs.</param>
    public MarkdownHtmlRenderer(MarkdownContentCatalog catalog, MarkdownRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        options ??= new MarkdownRenderOptions();
        if (options.HeadingIdentifierPolicy is not MarkdownHeadingIdentifierPolicy.Slug and not MarkdownHeadingIdentifierPolicy.None)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        _catalog = catalog;
        _headingIdentifierPolicy = options.HeadingIdentifierPolicy;
        _linkResolver = options.LinkResolver;
        _routeHrefFactory = options.RouteHrefFactory;
    }

    /// <summary>Produces an HTML fragment with deterministic identifiers and rewritten known page links.</summary>
    /// <param name="document">The document to render without mutation.</param>
    /// <param name="page">The page whose asset directory anchors relative links.</param>
    /// <returns>The rendered HTML fragment; article wrapper classes apply only to node rendering.</returns>
    public string Render(MarkdownDocument document, MarkdownPage page)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(page);
        string html = MarkdownText.ToHtml(document);
        var identifiers = new MarkdownHeadingIdentifiers();
        int searchIndex = 0;
        foreach (MarkdownNode node in Enumerate(document))
        {
            if (node is MarkdownHeading heading && _headingIdentifierPolicy == MarkdownHeadingIdentifierPolicy.Slug)
            {
                string marker = "<h" + heading.Level.ToString(CultureInfo.InvariantCulture) + ">";
                int markerIndex = html.IndexOf(marker, searchIndex, StringComparison.Ordinal);
                if (markerIndex < 0)
                {
                    throw new InvalidOperationException("The Markdown HTML renderer did not emit the expected heading.");
                }

                int insertionIndex = markerIndex + marker.Length - 1;
                string attribute = " id=\"" + identifiers.Create(heading.Inlines) + "\"";
                html = html.Insert(insertionIndex, attribute);
                searchIndex = insertionIndex + attribute.Length + 1;
            }
            else if (node is MarkdownLink link)
            {
                const string marker = "<a href=\"";
                int markerIndex = html.IndexOf(marker, searchIndex, StringComparison.Ordinal);
                if (markerIndex < 0)
                {
                    throw new InvalidOperationException("The Markdown HTML renderer did not emit the expected anchor.");
                }

                int start = markerIndex + marker.Length;
                int end = html.IndexOf('"', start);
                MarkdownLinkResolution resolution = _linkResolver?.Invoke(page, link.Destination)
                    ?? MarkdownLinks.Resolve(_catalog, page, link.Destination);
                string destination = resolution.IsInternal
                    ? _routeHrefFactory?.Invoke(resolution.Destination) ?? resolution.Destination
                    : resolution.Destination;
                string href = EncodeHref(destination);
                html = html[..start] + href + html[end..];
                searchIndex = start + href.Length + 1;
            }
        }

        return html;
    }

    private static IEnumerable<MarkdownNode> Enumerate(MarkdownDocument document)
    {
        var pending = new Stack<MarkdownNode>();
        Push(pending, document.Blocks);
        while (pending.TryPop(out MarkdownNode? node))
        {
            yield return node;
            switch (node)
            {
                case MarkdownHeading heading:
                    Push(pending, heading.Inlines);
                    break;
                case MarkdownParagraph paragraph:
                    Push(pending, paragraph.Inlines);
                    break;
                case MarkdownBlockQuote quote:
                    Push(pending, quote.Blocks);
                    break;
                case MarkdownList list:
                    Push(pending, list.Items);
                    break;
                case MarkdownListItem item:
                    Push(pending, item.Blocks);
                    break;
                case MarkdownLink link:
                    Push(pending, link.Inlines);
                    break;
                case MarkdownEmphasis emphasis:
                    Push(pending, emphasis.Inlines);
                    break;
                case MarkdownStrong strong:
                    Push(pending, strong.Inlines);
                    break;
            }
        }
    }

    private static void Push<T>(Stack<MarkdownNode> pending, IList<T> nodes)
        where T : MarkdownNode
    {
        for (int index = nodes.Count - 1; index >= 0; index--)
        {
            pending.Push(nodes[index]);
        }
    }

    private static string EncodeHref(string destination)
    {
        const string safe = "!#$%&'()*+,-./0123456789:;=?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[]_abcdefghijklmnopqrstuvwxyz~";
        var builder = new StringBuilder(destination.Length);
        Span<byte> buffer = stackalloc byte[4];
        foreach (Rune rune in destination.EnumerateRunes())
        {
            if (rune.IsAscii && safe.Contains((char)rune.Value))
            {
                builder.Append(rune.Value switch
                {
                    '&' => "&amp;",
                    '\'' => "&#39;",
                    _ => ((char)rune.Value).ToString(),
                });
                continue;
            }

            int count = rune.EncodeToUtf8(buffer);
            for (int index = 0; index < count; index++)
            {
                builder.Append('%').Append(buffer[index].ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }
}
