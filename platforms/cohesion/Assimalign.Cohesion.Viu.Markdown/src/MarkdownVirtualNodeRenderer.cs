using System;
using System.Collections.Generic;
using System.Globalization;

using Assimalign.Cohesion.Content.Markdown;
using Assimalign.Cohesion.Viu.Markdown.Internal;
using Assimalign.Viu.Components;
using Assimalign.Viu.Router;

namespace Assimalign.Cohesion.Viu.Markdown;

/// <summary>
/// Converts the closed Markdown node family into immutable Viu nodes without changing the document.
/// Internal page links invoke RouterLink; literal HTML remains text and never becomes raw markup.
/// </summary>
/// <remarks>
/// Traversal uses an explicit stack, including nested inline content. Each render owns its heading
/// counts and output collections. Callback thread safety remains the caller's responsibility.
/// </remarks>
public sealed class MarkdownVirtualNodeRenderer
{
    private static readonly ComponentReference RouterLinkReference = ComponentReference.ForType(typeof(RouterLink));
    private readonly MarkdownContentCatalog _catalog;
    private readonly string? _articleClass;
    private readonly MarkdownHeadingIdentifierPolicy _headingIdentifierPolicy;
    private readonly Func<MarkdownPage, string, MarkdownLinkResolution?>? _linkResolver;

    /// <summary>Initializes a renderer that snapshots options and borrows an immutable catalog.</summary>
    /// <param name="catalog">The content catalog used for internal link lookup.</param>
    /// <param name="options">The rendering policy, or null for default article classes and heading slugs.</param>
    public MarkdownVirtualNodeRenderer(MarkdownContentCatalog catalog, MarkdownRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        options ??= new MarkdownRenderOptions();
        if (options.HeadingIdentifierPolicy is not MarkdownHeadingIdentifierPolicy.Slug and not MarkdownHeadingIdentifierPolicy.None)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        _catalog = catalog;
        _articleClass = options.ArticleClass;
        _headingIdentifierPolicy = options.HeadingIdentifierPolicy;
        _linkResolver = options.LinkResolver;
    }

    /// <summary>
    /// Renders an article tree with fresh heading identifiers, copied node collections, and a
    /// data-markdown-route attribute identifying the catalog page whose content has finished rendering.
    /// </summary>
    /// <param name="document">The document to read without mutation.</param>
    /// <param name="page">The page whose asset path anchors relative links.</param>
    /// <returns>An immutable article element containing the rendered document.</returns>
    public VirtualNode Render(MarkdownDocument document, MarkdownPage page)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(page);
        var identifiers = new MarkdownHeadingIdentifiers();
        var output = new List<VirtualNode>();
        var pending = new Stack<RenderOperation>();
        Push(pending, document.Blocks, output);
        while (pending.TryPop(out RenderOperation operation))
        {
            if (operation.Complete is not null)
            {
                operation.Output.Add(operation.Complete());
                continue;
            }

            switch (operation.Node)
            {
                case MarkdownParagraph paragraph:
                    if (operation.Tight)
                    {
                        Push(pending, paragraph.Inlines, operation.Output);
                    }
                    else
                    {
                        Container(pending, operation, paragraph.Inlines, children => Element("p", children));
                    }

                    break;
                case MarkdownHeading heading:
                    string? identifier = _headingIdentifierPolicy == MarkdownHeadingIdentifierPolicy.Slug
                        ? identifiers.Create(heading.Inlines)
                        : null;
                    Container(pending, operation, heading.Inlines, children => Element(
                        "h" + heading.Level.ToString(CultureInfo.InvariantCulture),
                        children,
                        identifier is null ? null : [Attribute("id", identifier)]));
                    break;
                case MarkdownBlockQuote quote:
                    Container(pending, operation, quote.Blocks, children => Element("blockquote", children));
                    break;
                case MarkdownList list:
                    Container(pending, operation, list.Items, children => Element(
                        list.IsOrdered ? "ol" : "ul",
                        children,
                        list.IsOrdered && list.Start != 1
                            ? [Attribute("start", list.Start.ToString(CultureInfo.InvariantCulture))]
                            : null), list.IsTight);
                    break;
                case MarkdownListItem item:
                    Container(pending, operation, item.Blocks, children => Element("li", children), operation.Tight);
                    break;
                case MarkdownCodeBlock code:
                    string language = FirstWord(code.Info);
                    operation.Output.Add(Element("pre", [Element(
                        "code",
                        [new TextNode(code.Literal)],
                        language.Length == 0 ? null : [Attribute("class", "language-" + language)])]));
                    break;
                case MarkdownThematicBreak:
                    operation.Output.Add(Element("hr"));
                    break;
                case MarkdownLiteral literal:
                    operation.Output.Add(new TextNode(literal.Text));
                    break;
                case MarkdownEmphasis emphasis:
                    Container(pending, operation, emphasis.Inlines, children => Element("em", children));
                    break;
                case MarkdownStrong strong:
                    Container(pending, operation, strong.Inlines, children => Element("strong", children));
                    break;
                case MarkdownCodeSpan span:
                    operation.Output.Add(Element("code", [new TextNode(span.Literal)]));
                    break;
                case MarkdownLineBreak lineBreak:
                    operation.Output.Add(lineBreak.IsHard ? Element("br") : new TextNode("\n"));
                    break;
                case MarkdownLink link:
                    MarkdownLinkResolution resolution = _linkResolver?.Invoke(page, link.Destination)
                        ?? MarkdownLinks.Resolve(_catalog, page, link.Destination);
                    Container(pending, operation, link.Inlines, children => Link(resolution, link.Title, children));
                    break;
                case MarkdownImage image:
                    List<ElementBinding> imageBindings =
                    [
                        Attribute("src", image.Destination),
                        Attribute("alt", MarkdownHeadingIdentifiers.Flatten(image.Inlines)),
                    ];
                    if (image.Title is not null)
                    {
                        imageBindings.Add(Attribute("title", image.Title));
                    }

                    operation.Output.Add(Element("img", bindings: imageBindings));
                    break;
                default:
                    throw new InvalidOperationException("The Markdown document contains an unsupported node.");
            }
        }

        List<ElementBinding> articleBindings = [Attribute("data-markdown-route", page.RoutePath)];
        if (_articleClass is not null)
        {
            articleBindings.Add(Attribute("class", _articleClass));
        }

        return Element("article", output, articleBindings);
    }

    private static VirtualNode Link(MarkdownLinkResolution resolution, string? title, List<VirtualNode> children)
    {
        if (!resolution.IsInternal)
        {
            List<ElementBinding> bindings = [Attribute("href", resolution.Destination)];
            if (title is not null)
            {
                bindings.Add(Attribute("title", title));
            }

            return Element("a", children, bindings);
        }

        var content = new FragmentNode(children);
        var arguments = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["to"] = resolution.Destination,
        };
        if (title is not null)
        {
            arguments.Add("title", title);
        }

        return new ComponentNode(RouterLinkReference, new ComponentInvocation(
            arguments: arguments,
            slots: new Dictionary<string, ComponentSlot>(StringComparer.Ordinal)
            {
                ["default"] = _ => content,
            }));
    }

    private static void Container<T>(
        Stack<RenderOperation> pending,
        RenderOperation operation,
        IList<T> nodes,
        Func<List<VirtualNode>, VirtualNode> create,
        bool tight = false)
        where T : MarkdownNode
    {
        var children = new List<VirtualNode>();
        pending.Push(new RenderOperation(null, operation.Output, false, () => create(children)));
        Push(pending, nodes, children, tight);
    }

    private static void Push<T>(
        Stack<RenderOperation> pending,
        IList<T> nodes,
        List<VirtualNode> output,
        bool tight = false)
        where T : MarkdownNode
    {
        for (int index = nodes.Count - 1; index >= 0; index--)
        {
            pending.Push(new RenderOperation(nodes[index], output, tight));
        }
    }

    private static ElementNode Element(
        string name,
        IEnumerable<VirtualNode>? children = null,
        IEnumerable<ElementBinding>? bindings = null) => new(new QualifiedName(name), bindings, children);

    private static ElementBinding Attribute(string name, string value) => ElementBinding.Attribute(new QualifiedName(name), value);

    private static string FirstWord(string? info)
    {
        ReadOnlySpan<char> text = info.AsSpan().Trim();
        int end = text.IndexOfAny(' ', '\t');
        return (end < 0 ? text : text[..end]).ToString();
    }

    private readonly record struct RenderOperation(
        MarkdownNode? Node,
        List<VirtualNode> Output,
        bool Tight = false,
        Func<VirtualNode>? Complete = null);
}
