using System;
using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Cohesion.Viu.Markdown.Tests;

public sealed class CatalogTests
{
    [Fact]
    public void ExtractsMetadataRoutesAndHierarchicalNavigationOrder()
    {
        MarkdownContentCatalogBuilder builder = new("content/", "/manual/");
        MarkdownContentCatalog catalog = builder
            .Add("guide/last.md", "# Last\n\nLast description.")
            .Add("guide/index.md", "# User Guide\n\nOverview.\n\n[Second](second.md) [First](first.md)")
            .Add("guide/first.md", "# First\n\nA wrapped\ndescription.\n\n> **Status:** Partial.\n> More detail.")
            .Add("index.md", "# Home\n\nWelcome.\n\n[Guide](guide/index.md)")
            .Add("guide/second.md", "# Second\n\nSecond description.")
            .Build();

        catalog.Pages.Select(page => page.RoutePath).ShouldBe([
            "/manual", "/manual/guide", "/manual/guide/second", "/manual/guide/first", "/manual/guide/last"]);
        catalog.TryGetPageByRoute(" /manual\\guide\\first/ ", out MarkdownPage? first).ShouldBeTrue();
        first.ShouldNotBeNull();
        first.Description.ShouldBe("A wrapped description.");
        first.Status.ShouldBe("Partial. More detail.");
        first.AssetPath.ShouldBe("content/guide/first.md");
        first.SourceRelativePath.ShouldBe("guide/first.md");
        first.Order.ShouldBe(2);
        first.Section.ShouldBeSameAs(catalog.Sections[0].Sections[0]);
        first.Section!.Title.ShouldBe("User Guide");
        catalog.TryGetPageByAssetPath("/content\\guide/first.md", out MarkdownPage? asset).ShouldBeTrue();
        asset.ShouldBeSameAs(first);
        catalog.TryGetPageBySourcePath("guide\\first.md", out MarkdownPage? source).ShouldBeTrue();
        source.ShouldBeSameAs(first);
        catalog.TryGetPageBySourcePath("content/guide/first.md", out _).ShouldBeFalse();
        catalog.TryGetPageByRoute("/MANUAL/guide/first", out _).ShouldBeFalse();
        catalog.TryGetPageByAssetPath("Content/guide/first.md", out _).ShouldBeFalse();
        builder.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void FallbacksAreOrdinalAndUnlinkedPagesRemainVisible()
    {
        MarkdownContentCatalogBuilder builder = new();
        MarkdownContentCatalog catalog = builder
            .Add("odd-folder/zebra.md", "No heading\n\nDescription.")
            .Add("odd-folder/apple.md", "# Apple")
            .Add("odd-folder/middle.md", "# Middle")
            .Build();

        catalog.Pages.Select(page => page.Title).ShouldBe(["Apple", "Middle", "Zebra"]);
        catalog.Sections[0].Sections[0].Title.ShouldBe("Odd Folder");
        builder.Diagnostics.Single().Identifier.ShouldBe("VIUMD002");
        builder.Add("odd-folder/banana.md", "# Banana");
        builder.Build().Pages.Select(page => page.Title).ShouldBe(["Apple", "Banana", "Middle", "Zebra"]);
        catalog.Pages.Count.ShouldBe(3);
    }

    [Fact]
    public void ReportsDuplicateRoutesAndBrokenIndexLinksDeterministically()
    {
        MarkdownContentCatalogBuilder builder = new();
        builder.Add("guide.md", "# Guide")
            .Add("guide/index.md", "# Nested Guide\n\nDescription.\n\n[Missing](missing.md)");
        Should.Throw<InvalidOperationException>(() => builder.Build()).Message.ShouldContain("/guide");
        builder.Diagnostics.Select(diagnostic => diagnostic.Identifier).ShouldBe(["VIUMD001", "VIUMD003"]);
        builder.Diagnostics[0].IsError.ShouldBeTrue();
        builder.Diagnostics[1].IsError.ShouldBeFalse();
    }

    [Fact]
    public void IndexOrderingResolvesSegmentsQueriesFragmentsAndIgnoresCodeExamples()
    {
        MarkdownContentCatalogBuilder builder = new();
        MarkdownContentCatalog catalog = builder
            .Add("index.md", "# Home\n\nDescription.\n\n```md\n[Fake](missing.md)\n```\n" +
                "`[Fake](missing.md)` [External](https://example.test/file.md)\n" +
                "[Second](./two.md?view=full#topic) [First](folder/../one.md)")
            .Add("one.md", "# One")
            .Add("two.md", "# Two")
            .Build();
        catalog.Pages.Select(page => page.RoutePath).ShouldBe(["/", "/two", "/one"]);
        builder.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void NavigationInterleavesDirectPagesAndNestedSectionsInIndexOrder()
    {
        MarkdownContentCatalog catalog = new MarkdownContentCatalogBuilder()
            .Add("index.md", "# Home\n\nDescription.\n\n[First](first.md) [Guide](guide/index.md) [Last](last.md)")
            .Add("first.md", "# First")
            .Add("last.md", "# Last")
            .Add("guide/index.md", "# Guide\n\nDescription.\n\n[Page](page.md)")
            .Add("guide/page.md", "# Page")
            .Build();
        catalog.Pages.Select(page => page.RoutePath).ShouldBe(["/", "/first", "/guide", "/guide/page", "/last"]);
        catalog.Sections[0].Pages.Select(page => page.Order).ShouldBe([0, 1, 3]);
        catalog.Sections[0].Sections.Single().Order.ShouldBe(2);
    }

    [Fact]
    public void NavigationIgnoresImagesMalformedLinksAndVariableLengthInlineCodeSpans()
    {
        MarkdownContentCatalogBuilder builder = new();
        MarkdownContentCatalog catalog = builder
            .Add("index.md", "# Home\n\nDescription.\n\n" +
                "![Image](missing.md) malformed](missing.md) ``code ` [Fake](missing.md)``\n" +
                "![Image](one.md) [Two](two.md) [One](one.md)")
            .Add("one.md", "# One")
            .Add("two.md", "# Two")
            .Build();
        builder.Diagnostics.ShouldBeEmpty();
        catalog.Pages.Select(page => page.RoutePath).ShouldBe(["/", "/two", "/one"]);
    }

    [Fact]
    public void ExplicitEntriesPreserveMetadataAndSectionReferencesWithoutReadingFiles()
    {
        MarkdownContentCatalog catalog = new MarkdownContentCatalogBuilder()
            .Add(new MarkdownPageEntry("/first", "docs/area/first.md", "Explicit", "Summary", "Ready", Order: 1))
            .Add(new MarkdownPageEntry("/second", "docs/area/second.md", "Second", Order: 0))
            .Build();
        catalog.Pages.Select(page => page.Title).ShouldBe(["Second", "Explicit"]);
        catalog.Pages[1].Description.ShouldBe("Summary");
        catalog.Pages[1].Status.ShouldBe("Ready");
        catalog.Pages[1].SourceRelativePath.ShouldBe("area/first.md");
        catalog.Pages[1].Section!.Pages[1].ShouldBeSameAs(catalog.Pages[1]);
    }

    [Theory]
    [InlineData("# C#", "C#", false)]
    [InlineData("# C# ###", "C#", false)]
    [InlineData("# Topic###", "Topic###", false)]
    [InlineData("# ###", "Fallback", true)]
    [InlineData("# ", "Fallback", true)]
    public void TitlesPreserveLiteralHashesAndDiagnoseEmptyHeadings(string markdown, string title, bool warning)
    {
        MarkdownContentCatalogBuilder builder = new();
        builder.Add("fallback.md", markdown).Build().Pages.Single().Title.ShouldBe(title);
        builder.Diagnostics.Any(diagnostic => diagnostic.Identifier == "VIUMD002").ShouldBe(warning);
    }

    [Theory]
    [InlineData("", "/")]
    [InlineData("/", "/")]
    [InlineData(" guide\\page/ ", "/guide/page")]
    [InlineData("///guide//page///", "/guide//page")]
    public void RouteNormalizationMatchesTheExistingOrdinalContract(string input, string expected)
        => MarkdownContentCatalog.NormalizeRoute(input).ShouldBe(expected);
}
