using System;
using System.Collections.Generic;
using System.Linq;

using Assimalign.Cohesion.Content.Markdown;
using Assimalign.Viu.Components;
using Assimalign.Viu.Router;

using Shouldly;

using Xunit;

namespace Assimalign.Cohesion.Viu.Markdown.Tests;

public sealed class MarkdownRenderingTests
{
    [Theory]
    [InlineData("#heading", "#heading", false)]
    [InlineData("./target.md#heading", "/guide/target#heading", true)]
    [InlineData("target.md?source=menu#heading", "/guide/target#heading", true)]
    [InlineData("../index.md", "/", true)]
    [InlineData("/docs/guide/target.md", "/guide/target", true)]
    [InlineData("target.MD", "target.MD", false)]
    [InlineData("missing.md?query#fragment", "missing.md?query#fragment", false)]
    [InlineData("image.png", "image.png", false)]
    [InlineData("/guide/target", "/guide/target", false)]
    [InlineData("https://example.test/target.md", "https://example.test/target.md", false)]
    [InlineData("//example.test/target.md", "//example.test/target.md", false)]
    [InlineData("mailto:someone@example.test", "mailto:someone@example.test", false)]
    public void LinkResolutionPreservesTheDocumentNavigationContract(string destination, string expected, bool isInternal)
    {
        MarkdownContentCatalog catalog = Catalog();
        MarkdownLinkResolution resolution = MarkdownLinks.Resolve(catalog, Page(catalog), destination);
        resolution.Destination.ShouldBe(expected);
        resolution.IsInternal.ShouldBe(isInternal);
    }

    [Theory]
    [InlineData("docs/guide/current.md", "../index.md", "docs/index.md")]
    [InlineData("docs/guide/current.md", "./nested/../target.md", "docs/guide/target.md")]
    [InlineData("docs/guide/current.md", "/docs//./index.md", "docs/index.md")]
    [InlineData("docs/current.md", "../../../index.md", "index.md")]
    [InlineData("current.md", "target.md", "target.md")]
    public void AssetResolutionUsesSegments(string current, string destination, string expected)
        => MarkdownLinks.ResolveAssetPath(current, destination).ShouldBe(expected);

    [Fact]
    public void InternalLinksCarryRouterArgumentsAndImmutableDefaultSlots()
    {
        ElementNode root = Render("[**Next**](target.md#details \"Read next\") [External](https://example.test \"Website\") [Here](#details)");
        ElementNode paragraph = root.Children.Single().ShouldBeOfType<ElementNode>();
        ComponentNode link = paragraph.Children[0].ShouldBeOfType<ComponentNode>();
        link.Component.ShouldBe(ComponentReference.ForType(typeof(RouterLink)));
        link.Invocation.Arguments["to"].ShouldBe("/guide/target#details");
        link.Invocation.Arguments["title"].ShouldBe("Read next");
        FragmentNode slot = link.Invocation.Slots["default"](new Dictionary<string, object?>()).ShouldBeOfType<FragmentNode>();
        slot.Children[0].ShouldBeOfType<ElementNode>().Name.LocalName.ShouldBe("strong");
        ElementNode external = paragraph.Children[2].ShouldBeOfType<ElementNode>();
        external.Name.LocalName.ShouldBe("a");
        Attribute(external, "href").ShouldBe("https://example.test");
        Attribute(external, "title").ShouldBe("Website");
        Attribute(paragraph.Children[4].ShouldBeOfType<ElementNode>(), "href").ShouldBe("#details");
    }

    [Fact]
    public void EveryBlockAndInlineKindProducesHostNodes()
    {
        ElementNode root = Render("""
            # Heading

            Paragraph *emphasis* **strong** `code` ![An *image*](image.png "Image title")
            soft  
            hard

            > A quote

            3. Ordered
            4. Next

            - Bullet

            ```csharp linenums
            <literal>();
            ```

            ---
            """);
        root.Name.LocalName.ShouldBe("article");
        Attribute(root, "data-markdown-route").ShouldBe("/guide/current");
        Attribute(root, "class").ShouldBe("markdown-content");
        root.Children.Cast<ElementNode>().Select(node => node.Name.LocalName)
            .ShouldBe(["h1", "p", "blockquote", "ol", "ul", "pre", "hr"]);
        ElementNode paragraph = root.Children[1].ShouldBeOfType<ElementNode>();
        paragraph.Children.OfType<ElementNode>().Select(node => node.Name.LocalName)
            .ShouldBe(["em", "strong", "code", "img", "br"]);
        paragraph.Children.OfType<TextNode>().Any(node => node.Text == "\n").ShouldBeTrue();
        ElementNode image = paragraph.Children.OfType<ElementNode>().Single(node => node.Name.LocalName == "img");
        Attribute(image, "src").ShouldBe("image.png");
        Attribute(image, "alt").ShouldBe("An image");
        Attribute(image, "title").ShouldBe("Image title");
        Attribute(root.Children[3].ShouldBeOfType<ElementNode>(), "start").ShouldBe("3");
        ElementNode code = root.Children[5].ShouldBeOfType<ElementNode>().Children[0].ShouldBeOfType<ElementNode>();
        code.Name.LocalName.ShouldBe("code");
        Attribute(code, "class").ShouldBe("language-csharp");
        code.Children[0].ShouldBeOfType<TextNode>().Text.ShouldBe("<literal>();\n");
    }

    [Fact]
    public void TightListsFlattenOnlyDirectItemParagraphs()
    {
        var quote = new MarkdownBlockQuote();
        quote.Blocks.Add(Paragraph("quoted"));
        var tight = new MarkdownList(false);
        var item = new MarkdownListItem();
        item.Blocks.Add(Paragraph("bare"));
        item.Blocks.Add(quote);
        tight.Items.Add(item);
        var loose = new MarkdownList(true) { IsTight = false };
        var looseItem = new MarkdownListItem();
        looseItem.Blocks.Add(Paragraph("wrapped"));
        loose.Items.Add(looseItem);
        var document = new MarkdownDocument();
        document.Add(tight);
        document.Add(loose);
        ElementNode root = Render(document);
        ElementNode tightItem = root.Children[0].ShouldBeOfType<ElementNode>().Children[0].ShouldBeOfType<ElementNode>();
        tightItem.Children[0].ShouldBeOfType<TextNode>().Text.ShouldBe("bare");
        tightItem.Children[1].ShouldBeOfType<ElementNode>().Children[0].ShouldBeOfType<ElementNode>().Name.LocalName.ShouldBe("p");
        root.Children[1].ShouldBeOfType<ElementNode>().Children[0].ShouldBeOfType<ElementNode>()
            .Children[0].ShouldBeOfType<ElementNode>().Name.LocalName.ShouldBe("p");
    }

    [Fact]
    public void HeadingSlugsFlattenInlineContentAndResetDuplicateCountsOnEveryRender()
    {
        MarkdownDocument document = MarkdownText.Parse("# API `Code` [Links](target.md) *And* **Text**\n\n# API Code Links And Text\n\n> ## !!!\n\n# !!!\n\n# Café -- déjà_vu!");
        ElementNode root = Render(document);
        Attribute(root.Children[0].ShouldBeOfType<ElementNode>(), "id").ShouldBe("api-code-links-and-text");
        Attribute(root.Children[1].ShouldBeOfType<ElementNode>(), "id").ShouldBe("api-code-links-and-text-1");
        Attribute(root.Children[2].ShouldBeOfType<ElementNode>().Children[0].ShouldBeOfType<ElementNode>(), "id").ShouldBe("section");
        Attribute(root.Children[3].ShouldBeOfType<ElementNode>(), "id").ShouldBe("section-1");
        Attribute(root.Children[4].ShouldBeOfType<ElementNode>(), "id").ShouldBe("café-déjàvu");
        Attribute(Render(document).Children[0].ShouldBeOfType<ElementNode>(), "id").ShouldBe("api-code-links-and-text");
    }

    [Fact]
    public void OptionsAreSnapshottedAndLinkHooksCanOverrideResolution()
    {
        MarkdownContentCatalog catalog = Catalog();
        var options = new MarkdownRenderOptions
        {
            ArticleClass = "prose documentation",
            HeadingIdentifierPolicy = MarkdownHeadingIdentifierPolicy.None,
            LinkResolver = (_, destination) => destination == "custom" ? new("/custom", true) : null,
        };
        var renderer = new MarkdownVirtualNodeRenderer(catalog, options);
        options.ArticleClass = "changed";
        options.HeadingIdentifierPolicy = MarkdownHeadingIdentifierPolicy.Slug;
        ElementNode root = renderer.Render(MarkdownText.Parse("# Title\n\n[Custom](custom) [Normal](target.md)"), Page(catalog)).ShouldBeOfType<ElementNode>();
        Attribute(root, "class").ShouldBe("prose documentation");
        root.Children[0].ShouldBeOfType<ElementNode>().Bindings.ShouldBeEmpty();
        root.Children[1].ShouldBeOfType<ElementNode>().Children[0].ShouldBeOfType<ComponentNode>().Invocation.Arguments["to"].ShouldBe("/custom");
        root.Children[1].ShouldBeOfType<ElementNode>().Children[2].ShouldBeOfType<ComponentNode>().Invocation.Arguments["to"].ShouldBe("/guide/target");
    }

    [Fact]
    public void HtmlHelperRewritesLinksAndHeadingsWithoutMutatingTheDocument()
    {
        MarkdownContentCatalog catalog = Catalog();
        MarkdownDocument document = MarkdownText.Parse("# [Title](target.md)\n\n[Next](target.md?query#part) [Here](#part) ![Alt](image.png)\n\n# Title");
        string before = MarkdownText.ToHtml(document);
        var renderer = new MarkdownHtmlRenderer(catalog, new MarkdownRenderOptions { RouteHrefFactory = route => "#" + route });
        string html = renderer.Render(document, Page(catalog));
        html.ShouldContain("<h1 id=\"title\"><a href=\"#/guide/target\">Title</a></h1>");
        html.ShouldContain("href=\"#/guide/target#part\"");
        html.ShouldContain("href=\"#part\"");
        html.ShouldContain("<h1 id=\"title-1\">Title</h1>");
        html.ShouldContain("src=\"image.png\"");
        MarkdownText.ToHtml(document).ShouldBe(before);
    }

    [Fact]
    public void HtmlHelperEncodesCallbackDestinationsAndSkipsLinksInsideImageAltText()
    {
        MarkdownContentCatalog catalog = Catalog();
        var image = new MarkdownImage("image.png");
        var hiddenLink = new MarkdownLink("target.md");
        hiddenLink.Inlines.Add(new MarkdownLiteral("alternative"));
        image.Inlines.Add(hiddenLink);
        var paragraph = new MarkdownParagraph();
        paragraph.Inlines.Add(image);
        var link = new MarkdownLink("target.md");
        link.Inlines.Add(new MarkdownLiteral("visible"));
        paragraph.Inlines.Add(link);
        var document = new MarkdownDocument();
        document.Add(paragraph);
        var renderer = new MarkdownHtmlRenderer(catalog, new MarkdownRenderOptions
        {
            LinkResolver = (_, _) => new("/a b?x=1&y='é'", true),
        });
        string html = renderer.Render(document, Page(catalog));
        html.ShouldContain("alt=\"alternative\"");
        html.ShouldContain("href=\"/a%20b?x=1&amp;y=&#39;%C3%A9&#39;\"");
    }

    [Fact]
    public void RawHtmlRemainsLiteralText()
    {
        MarkdownDocument document = MarkdownText.Parse("<script>alert('raw')</script>");
        ElementNode paragraph = Render(document).Children[0].ShouldBeOfType<ElementNode>();
        paragraph.Children.All(node => node is TextNode).ShouldBeTrue();
        string.Join(string.Empty, paragraph.Children.Cast<TextNode>().Select(node => node.Text))
            .ShouldBe("<script>alert('raw')</script>");
        MarkdownContentCatalog catalog = Catalog();
        new MarkdownHtmlRenderer(catalog).Render(document, Page(catalog)).ShouldContain("&lt;script&gt;");
    }

    [Fact]
    public void ArticleIdentityTracksTheRenderedPageEvenWithoutClasses()
    {
        MarkdownContentCatalog catalog = Catalog();
        var renderer = new MarkdownVirtualNodeRenderer(catalog, new MarkdownRenderOptions { ArticleClass = null });
        catalog.TryGetPageByRoute("/guide/target", out MarkdownPage? target).ShouldBeTrue();
        MarkdownDocument document = MarkdownText.Parse("# Shared heading");

        ElementNode currentArticle = renderer.Render(document, Page(catalog)).ShouldBeOfType<ElementNode>();
        ElementNode targetArticle = renderer.Render(document, target!).ShouldBeOfType<ElementNode>();

        Attribute(currentArticle, "data-markdown-route").ShouldBe("/guide/current");
        Attribute(targetArticle, "data-markdown-route").ShouldBe("/guide/target");
        targetArticle.Bindings.Count.ShouldBe(1);
        Attribute(targetArticle, "class").ShouldBeNull();
    }

    [Fact]
    public void DeepProgrammaticInlineTreesDoNotUseTheCallStack()
    {
        MarkdownInline inline = new MarkdownLiteral("deep");
        for (int index = 0; index < 5000; index++)
        {
            var emphasis = new MarkdownEmphasis();
            emphasis.Inlines.Add(inline);
            inline = emphasis;
        }

        var heading = new MarkdownHeading(1);
        heading.Inlines.Add(inline);
        var document = new MarkdownDocument();
        document.Add(heading);
        ElementNode rendered = Render(document).Children[0].ShouldBeOfType<ElementNode>();
        Attribute(rendered, "id").ShouldBe("deep");
        VirtualNode node = rendered.Children[0];
        int count = 0;
        while (node is ElementNode element)
        {
            count++;
            node = element.Children.Single();
        }

        count.ShouldBe(5000);
        node.ShouldBeOfType<TextNode>().Text.ShouldBe("deep");
    }

    private static MarkdownContentCatalog Catalog() => new MarkdownContentCatalogBuilder()
        .Add("index.md", "# Home\n\nHome description.")
        .Add("guide/current.md", "# Current\n\nCurrent description.")
        .Add("guide/target.md", "# Target\n\nTarget description.")
        .Build();

    private static MarkdownPage Page(MarkdownContentCatalog catalog)
    {
        catalog.TryGetPageByRoute("/guide/current", out MarkdownPage? page).ShouldBeTrue();
        return page!;
    }

    private static ElementNode Render(string markdown) => Render(MarkdownText.Parse(markdown));

    private static ElementNode Render(MarkdownDocument document)
    {
        MarkdownContentCatalog catalog = Catalog();
        return new MarkdownVirtualNodeRenderer(catalog).Render(document, Page(catalog)).ShouldBeOfType<ElementNode>();
    }

    private static string? Attribute(ElementNode node, string name)
        => node.Bindings.SingleOrDefault(binding => binding.Name.LocalName == name)?.Value as string;

    private static MarkdownParagraph Paragraph(string text)
    {
        var paragraph = new MarkdownParagraph();
        paragraph.Inlines.Add(new MarkdownLiteral(text));
        return paragraph;
    }
}
