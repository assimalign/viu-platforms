using System.Collections.Generic;

using Assimalign.Viu.Components;
using Assimalign.Viu.Router;

using Shouldly;

using Xunit;

namespace Assimalign.Cohesion.Viu.Markdown.Tests;

public sealed class MarkdownRoutesTests
{
    [Fact]
    public void CreatesOneNamedPageRecordWithFixedArgumentsPerCatalogPage()
    {
        MarkdownContentCatalog catalog = CreateCatalog();
        IReadOnlyList<RouteRecord> records = MarkdownRoutes.Create(catalog,
            new MarkdownRouteOptions { IncludeNotFoundRoute = false, NamePrefix = "docs:" });
        RouteMatcher matcher = new(records);

        records.Count.ShouldBe(catalog.Pages.Count);
        foreach (MarkdownPage page in catalog.Pages)
        {
            RouteLocation location = matcher.Resolve(page.RoutePath);
            location.Name.ShouldBe("docs:" + page.RoutePath);
            matcher.ResolveNamed(location.Name!).Path.ShouldBe(page.RoutePath);
            location.Route.ShouldNotBeNull();
            location.Route!.Path.ShouldBe(page.RoutePath);
            location.Route!.ArgumentsResolver!(location)!["route"].ShouldBe(page.RoutePath);
            location.Route!.Component.ShouldBeOfType<ComponentNode>()
                .Component.ShouldBe(MarkdownPageView.Registration.Reference);
        }
        matcher.Resolve("/guide/unknown").IsMatched.ShouldBeFalse();
        matcher.Resolve("/guide-extra").IsMatched.ShouldBeFalse();
    }

    [Theory]
    [InlineData("/guide#heading", "", "heading")]
    [InlineData("/guide?source=menu", "source=menu", "")]
    [InlineData("/guide?source=menu#heading", "source=menu", "heading")]
    [InlineData("/guide?#", "", "")]
    [InlineData("/guide#heading?text#more", "", "heading?text#more")]
    public void QueryAndFragmentSuffixesKeepTheExactPageAndItsFixedRoute(string fullPath, string query, string fragment)
    {
        RouteMatcher matcher = new(MarkdownRoutes.Create(CreateCatalog()));
        RouteLocation location = matcher.Resolve(fullPath);

        location.Path.ShouldBe("/guide");
        location.RawQuery.ShouldBe(query);
        location.Fragment.ShouldBe(fragment);
        location.FullPath.ShouldBe(fullPath);
        location.Name.ShouldBe("markdown:/guide");
        location.Route!.ArgumentsResolver!(location)!["route"].ShouldBe("/guide");
    }

    [Fact]
    public void LayoutProvidesParentAndDefaultChildAtDepthOne()
    {
        ComponentReference layout = ComponentReference.ForName("DocumentationLayout");
        IReadOnlyList<RouteRecord> records = MarkdownRoutes.Create(CreateCatalog(),
            new MarkdownRouteOptions { LayoutComponent = layout });
        RouteMatcher matcher = new(records);

        records.Count.ShouldBe(1);
        records[0].Path.ShouldBe("/");
        records[0].Component.ShouldBeOfType<ComponentNode>().Component.ShouldBe(layout);
        records[0].Children.Count.ShouldBe(3);
        records[0].Children[0].Path.ShouldBe(string.Empty);
        records[0].Children[1].Path.ShouldBe("/guide");
        foreach (string path in new[] { "/", "/#heading", "/guide", "/guide#heading" })
        {
            RouteLocation location = matcher.Resolve(path);
            location.Matched.Count.ShouldBe(2);
            location.Matched[0].ShouldBeSameAs(records[0]);
            location.Matched[1].Component.ShouldBeOfType<ComponentNode>()
                .Component.ShouldBe(MarkdownPageView.Registration.Reference);
        }
        matcher.ResolveNamed("markdown:/").Path.ShouldBe("/");
        matcher.ResolveNamed("markdown:/guide").Path.ShouldBe("/guide");
    }

    [Fact]
    public void CatchAllForwardsUnknownPathAndAcceptsCustomComponent()
    {
        ComponentReference notFound = ComponentReference.ForName("PageNotFound");
        RouteMatcher matcher = new(MarkdownRoutes.Create(CreateCatalog(),
            new MarkdownRouteOptions { NotFoundComponent = notFound }));
        RouteLocation location = matcher.Resolve("/missing/deep-page?source=menu#heading");

        location.Name.ShouldBe("markdown:not-found");
        location.Path.ShouldBe("/missing/deep-page");
        location.Fragment.ShouldBe("heading");
        location.Route!.Component.ShouldBeOfType<ComponentNode>().Component.ShouldBe(notFound);
        location.Route!.ArgumentsResolver!(location)!["route"].ShouldBe("/missing/deep-page");
    }

    [Fact]
    public void LayoutBaseRetainsAbsoluteCatalogRoutesAndNamedDefaultPage()
    {
        MarkdownContentCatalog catalog = new([new MarkdownSection("Docs", "", [
            new MarkdownPageEntry("/manual", "docs/index.md", "Home"),
            new MarkdownPageEntry("/manual/guide", "docs/guide.md", "Guide"),
        ])]);
        RouteMatcher matcher = new(MarkdownRoutes.Create(catalog, new MarkdownRouteOptions
        {
            LayoutComponent = ComponentReference.ForName("Layout"),
            LayoutPath = "/manual",
            IncludeNotFoundRoute = false,
        }));

        matcher.ResolveNamed("markdown:/manual").Path.ShouldBe("/manual");
        matcher.Resolve("/manual/guide").Matched.Count.ShouldBe(2);
        matcher.Resolve("/manual-extra").IsMatched.ShouldBeFalse();
    }

    private static MarkdownContentCatalog CreateCatalog()
        => new([new MarkdownSection("Docs", "", [
            new MarkdownPageEntry("/", "docs/index.md", "Home"),
            new MarkdownPageEntry("/guide", "docs/guide/index.md", "Guide"),
        ])]);
}
