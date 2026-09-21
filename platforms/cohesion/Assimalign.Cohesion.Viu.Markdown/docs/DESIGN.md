# Markdown content design

## Boundary and package placement

`Assimalign.Cohesion.Viu.Markdown` version `10.0.0-beta.5` is the sixth Cohesion Viu package. The
runtime references Hosting, published Cohesion Content.Markdown `10.0.1-preview.3.local`, and Viu Router,
Components, and Reactivity `10.0.0-beta.12`. It references no Viu Browser, platform Server, Cohesion
Web package, or Cohesion App shared framework. HTTP and file-system sources are ordinary .NET
implementations of one host-neutral contract. All registrations use instances and explicit
factories. Public runtime APIs inherit the repository's `EnablePreviewFeatures=true` policy and
the project enables `IsAotCompatible` and XML documentation generation.

## Build-time extraction

A Roslyn incremental generator was chosen over an MSBuild code-writing task. AdditionalFiles keep
the catalog visible to IDE compilation and permit incremental input tracking; generated output is
a declarative nested table of `MarkdownSection` and `MarkdownPageEntry` construction. There is no
runtime Markdown parsing to discover metadata. The nonpackable `netstandard2.0` generator ships in
the runtime package's `analyzers/dotnet/cs` directory. Its build props/targets supply configured
content items and compiler-visible metadata. It does not reference the `net10.0` Markdown library.
The generator is not preview-annotated because it runs in the Roslyn compiler host; this exception
does not change the runtime package's preview policy.

The generator and runtime builder compile the same internal, self-contained extraction source.
It examines lines according to the documentation convention: first-line H1, opening paragraph,
optional status blockquote, and relative `.md` links in each folder's index. Frontmatter is not a
second metadata system. Filenames derive routes; a physical root, asset prefix, and route base are
independent settings. Input ordering never changes output. Index links rank direct pages and child
sections; unlinked entries follow in ordinal order. Page and section order values share the
parent's navigation sequence, so depth-first catalog enumeration honors interleaved links to
direct pages and child sections. The catalog retains one root section so its
index title and pages are not lost. Applications can render that root separately when desired.

Diagnostics are `VIUMD001` (duplicate route, error), `VIUMD002` (missing first-line H1, warning with
filename fallback), `VIUMD003` (broken relative Markdown link in an index, warning), `VIUMD004`
(duplicate source, error), `VIUMD005` (invalid generated namespace, error), and `VIUMD006` (source
path escapes the content root, error). Warnings follow the
consumer's ordinary warning-as-error policy. Runtime diagnostics use the same extraction codes;
`Build` rejects errors. Explicit metadata construction remains available without the generator.

`MarkdownPageEntry` is the value-equal metadata input. `MarkdownSection` snapshots its ordered
entries, constructs pages with a reference back to that section, and snapshots its children.
`MarkdownContentCatalog` copies roots, enumerates the hierarchy, and rejects duplicate normalized
routes/assets. Comparisons are ordinal. Route normalization trims whitespace, translates separators,
and guarantees one leading slash; asset normalization only translates separators and trims leading
slashes, preserving the POC's behavior.

## Nodes, links, and identifiers

The renderer consumes the sealed Cohesion Markdown node family and creates immutable Viu node
descriptions. It maps block and inline forms, tight/loose list paragraphs, ordered-list starts,
fenced-code language classes, image alternate text, and duplicate heading slugs. Iterative traversal
keeps deep Markdown trees off the managed call stack. Per-render heading state never leaks across
pages, and constructor snapshots prevent later option mutations from changing a renderer.

The brief's `VirtualNodeFactory` is absent from both the published Viu beta.12 surface and the
read-only Viu sources. Supported `ElementNode`, `TextNode`, `FragmentNode`, and `ComponentNode`
constructors are the public construction seam; no replacement factory or upstream change is added.

Known relative Markdown links resolve by asset-directory segments (`.`, `..`, rooted paths), then
emit `RouterLink` component invocations with a `to` argument and immutable default-slot content.
Internal links with fragments navigate to `route#fragment`. Fragment-only and external links remain
anchors. Queries on known Markdown asset destinations are discarded, matching the prior content
pipeline; fragments are retained. Unknown assets and ordinary resource links are unchanged.
Applications can override link resolution. With web history and `<base href="/">`, hosts should
qualify fragment-only links with the current page route through `LinkResolver`: an ordinary
`href="#heading"` resolves against the document base, not the current nested route. Qualified links
may remain native anchors or be marked internal to use router navigation and `ScrollBehavior`.
The HTML helper calls
`MarkdownText.ToHtml`, rewrites emitted headings/anchors, and does not mutate the input document.
Its output remains ordinary HTML, so it does not confer component navigation semantics.

## Components, routing, and ownership

The code-first components declare static contracts and registrations, with explicit parameter
binding at setup/render. `[Parameter]` documents the template surface; Viu beta.12 only generates
attribute binding for single-file components, so runtime registration never scans these attributes.
`MarkdownComponents.Register` adds type and name references and preserves caller registrations.
`MarkdownNavigation` stays a small list renderer; applications retain control of shell layout.

`MarkdownPageView` resolves the catalog, content source, and node renderer from `ComponentContext.Services`.
Each load gets a request version and a linked cancellation source. Starting a different route
cancels the previous source; results and failures from superseded or unmounted reads cannot replace
the current tree, even when a custom source ignores cancellation. Reactive state changes trigger
normal rendering. Mount and update callbacks initiate loads; server prefetch awaits the same path.
Each instance belongs to one application event loop.

Routes carry fixed page arguments and unique names (`markdown:` plus the catalog route), with an
optional parent layout and a separately named catch-all. Page and layout records use plain catalog
paths; default children use an empty path so outlet depth remains correct. Viu beta.12 separates URL
suffixes before matching: `RouteLocation.Path` is the clean path, `RawQuery`/`Query` expose the query,
`Fragment` is raw text without `#`, and `FullPath` retains the raw suffix. Fixed page arguments and
catch-all arguments therefore contain only paths. Named routes resolve directly to catalog paths.

Hosts scroll through `ScrollBehavior` using `to.Fragment`, for example by returning
`new ScrollTarget("#" + to.Fragment)` after waiting for the destination's Markdown headings to render.
There is no implicit router fragment scrolling; decoding and CSS escaping are host policy. Navigation
can use `to.Path` to identify the page while preserving queries and fragments in the full location.
`MarkdownNavigation` marks the current page using its path, including when a query or fragment is
present; Viu's separate `RouterLink` exact-active state compares the full location.

HTTP clients and supplied source instances are borrowed. Factory registrations follow the host
provider's disposal rules. The hosting extension registers no implementation types. Consumers may
instead use any explicit service provider and component factory, or invoke the catalog/renderers
directly without hosting.

## Server-rendering seam and next phase

File-system content plus `MarkdownPageView.OnServerPrefetch` provides the host-neutral SSR path.
A Viu `ComponentHost` test awaits a page read before obtaining the immutable article tree. No
`Assimalign.Cohesion.Viu.Server` integration is wired here. The next phase is per-page request
composition in the Web host: select its router location, construct a fresh render application and
request scope, await prefetch, serialize the nodes, and choose document/head/status/cache policy.
Share immutable catalogs, but do not share request component or reactive lifetimes. Browser-only
history and scrolling remain the client host's responsibility.

## Verification boundaries

Unit tests exercise shared extraction, diagnostics, deterministic generator output, sources,
node/HTML rendering, link resolution, identifiers, real route matching, provider registration,
mounted component navigation/cancellation, and server prefetch. `scripts/Verify-MarkdownDocs.ps1`
builds an external consumer of the packed generator against the actual viu-docs Markdown tree and
compares its 45 routes/assets with the preserved POC fixture. The ordinary Release build runs AOT
compatibility analysis; it is not a claim that a NativeAOT or browser-WASM AOT publish was performed.

## Build host portability

Content-root normalization uses `MSBuild.NormalizePath(projectDirectory, contentRoot)` rather than
the two-argument `Path.GetFullPath`, because the latter is unavailable in Visual Studio's .NET
Framework MSBuild. This affects only path evaluation; the source generator's catalog contract is
unchanged. The family now composes Cohesion preview.3 through the complete `.local` feed identity.
