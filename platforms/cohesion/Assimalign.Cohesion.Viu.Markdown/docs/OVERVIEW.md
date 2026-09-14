# Assimalign.Cohesion.Viu.Markdown

Markdown content for Viu applications on Cohesion. Version `10.0.0-beta.4` composes the published
Cohesion Content.Markdown `10.0.0-beta.1` and Viu `10.0.0-beta.12` packages. The runtime targets
`net10.0`, enables trimming/AOT analysis, and contains no browser, Cohesion Web, Server, or shared
framework reference. Applications must enable preview features, matching the Cohesion hosting
packages' preview-annotated surface.

## Project setup

```xml
<PropertyGroup>
  <EnablePreviewFeatures>true</EnablePreviewFeatures>
  <ViuMarkdownContentRoot>$(MSBuildProjectDirectory)\..\..\docs</ViuMarkdownContentRoot>
  <ViuMarkdownContentPrefix>docs/</ViuMarkdownContentPrefix>
  <ViuMarkdownRouteBase>/</ViuMarkdownRouteBase>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="Assimalign.Cohesion.Viu.Markdown" Version="10.0.0-beta.4" />
  <Content Include="$(ViuMarkdownContentRoot)\**\*.md"
           Link="wwwroot\docs\%(RecursiveDir)%(Filename)%(Extension)" />
</ItemGroup>
```

The packaged Roslyn incremental generator emits `GeneratedMarkdownContent.Catalog` in `RootNamespace`.
Set `ViuMarkdownGeneratedNamespace` to override it. The default physical root is the project's
`docs` folder. `ViuMarkdownContentGlob` defaults to `**\*.md`; set
`ViuMarkdownEnableDefaultItems=false` and declare `ViuMarkdownContent` items for an explicit set.
Items flow to `AdditionalFiles` with their content-root-relative paths. The generator reads no files
outside those compiler inputs and emits no physical files itself.

Use lowercase kebab-case filenames. `index.md` maps to `/`, `guide/index.md` to `/guide`, and
`guide/quick-start.md` to `/guide/quick-start`. Asset paths retain the configured `docs/` prefix.
Put the page's H1 on line one, then a blank line and an opening description paragraph. An opening
`> **Status:** ...` blockquote supplies optional status. Relative `.md` links in a folder's index
order its navigation; unlinked pages and folders follow in ordinal order. No frontmatter is read.

## Cohesion hosting

```csharp
using Assimalign.Cohesion.Viu.Markdown;
using Assimalign.Viu.Components;

builder.AddMarkdownContent(options =>
{
    options.Catalog = GeneratedMarkdownContent.Catalog;
    options.Source = new HttpMarkdownContentSource(httpClient);
    options.RenderOptions = new MarkdownRenderOptions { ArticleClass = "markdown-body" };
});

var routes = MarkdownRoutes.Create(GeneratedMarkdownContent.Catalog,
    new MarkdownRouteOptions { LayoutComponent = ComponentReference.ForName("AppShell") });
```

The helper adds the catalog, source, node and HTML renderers to services and registers
`MarkdownPageView`, `MarkdownNavigation`, `RouterLink`, and `RouterView` by type and template name.
It preserves existing component registrations. Use the builder's concrete `ComponentFactory`, or
set `options.Components` explicitly; register generated application components in the same factory.
Alternatively supply `SourceFactory` to create a source using the final provider's dependencies.
Supply exactly one source instance or factory. Instance ownership remains with the caller;
factory-created disposable dependencies follow the host provider's lifetime.

Register a Viu `Router` over `routes` in services and select its history in the application. The
optional shell's nested outlet uses depth one. `<MarkdownPageView route="/guide"/>` also works
without the route helper. Each view loads on mount/update, cancels superseded reads and unmounts,
ignores stale completions, displays read failures, and awaits content during server prefetch.
`<MarkdownNavigation active-class="active"/>` renders the entire section tree; applications can
instead use the catalog to author their navigation.

## Without the hosting model

Compose any `System.IServiceProvider` using instances or explicit factories. For example, an
ordinary Cohesion provider can be built without `ViuApplicationBuilder`:

```csharp
using System;
using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Cohesion.Viu.Markdown;
using Assimalign.Viu.Components;

var catalog = GeneratedMarkdownContent.Catalog;
var services = new ServiceProviderBuilder();
services.Add(ServiceDescriptor.Singleton(catalog));
services.Add(ServiceDescriptor.Singleton<IMarkdownContentSource>(
    new HttpMarkdownContentSource(httpClient)));
services.Add(ServiceDescriptor.Singleton(new MarkdownVirtualNodeRenderer(catalog)));
IServiceProvider provider = ((IServiceProviderBuilder)services).Build();
var components = new ComponentFactory();
MarkdownComponents.Register(components);
```

Assign `provider` and `components` to the ordinary Viu application options and register its router
in that provider. The application owns provider/client disposal. The model and both renderers also
work directly, without services, components, or hosting.

`new MarkdownContentCatalogBuilder().Add("index.md", markdown).Build()` performs the same extraction
at runtime. `Add(MarkdownPageEntry)` accepts explicit metadata. `BuildAsync(source)` requires a source
listing: file-system sources provide one, while HTTP sources need an explicit asset-path list.
Use `new FileSystemMarkdownContentSource(directory, "docs/")` when the directory itself contains
`index.md` and the catalog's assets begin with `docs/`.

## Rendering policies

`MarkdownVirtualNodeRenderer.Render(document, page)` returns an immutable article tree. Heading IDs
flatten inline content, lower-case letters and digits, collapse whitespace/hyphens, fall back to
`section`, and suffix duplicates with `-1`, `-2`, and so on. Disable them through
`HeadingIdentifierPolicy=None`. `LinkResolver` can override a destination and explicitly select
internal RouterLink or ordinary-anchor behavior; null uses catalog resolution. Known relative `.md`
links navigate to `route#fragment`, preserving fragments and discarding queries to match the
documentation conventions. Unknown, non-Markdown, external, and fragment-only destinations remain
ordinary links. Raw HTML is literal
text in the underlying Markdown model and never becomes injected markup.

`MarkdownHtmlRenderer.Render(document, page)` keeps the Cohesion `MarkdownText.ToHtml` path available,
adds the same heading IDs and rewrites known links without mutating the document. It returns an HTML
fragment with ordinary anchors; `RouteHrefFactory` can format its internal hrefs for a chosen host.
The component always uses the node renderer.

Viu beta.12 matches plain page and layout paths after separating the URL suffix. `RouteLocation.Path`
contains only the path; `RawQuery`/`Query` and `Fragment` expose the suffix, while `FullPath` preserves
its original spelling. Named routes resolve to catalog paths. Hosts scroll through `ScrollBehavior`
using `to.Fragment`, for example `new ScrollTarget("#" + to.Fragment)` for a non-empty heading fragment
after waiting for asynchronous content to render. The router does not scroll to fragments implicitly.
Web history supports native fragment-only anchors when the document base remains the current URL.
With `<base href="/">`, qualify those links as `page.RoutePath + destination` through `LinkResolver`.
They may remain native anchors or be marked internal to use router navigation and `ScrollBehavior`.
See [the design document](https://github.com/assimalign/viu-platforms/blob/main/platforms/cohesion/Assimalign.Cohesion.Viu.Markdown/docs/DESIGN.md)
for the server-rendering boundary and known upstream seams.
