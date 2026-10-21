# Viu platform integrations

This repository packages host-platform integrations for Viu. The Cohesion family adds an
optional Cohesion application host around Viu, a browser host, Cohesion Web asset and
server-rendering middleware, Markdown content, a development server, and `dotnet new` templates. Ordinary Viu
composition remains independent of these packages.

## Cohesion packages

- `Assimalign.Cohesion.Viu.Hosting` — host-neutral `ViuApplication` and
  `ViuApplicationBuilder`, backed by Cohesion dependency injection, configuration, and hosting.
- `Assimalign.Cohesion.Viu.Hosting.Browser` — the explicit browser host registration that mounts
  through `Assimalign.Viu.Browser`.
- `Assimalign.Cohesion.Viu.Routing` — opt-in `AddRouting`/`AddRoutes` service registration and
  `UseRouter` lifetime installation for the Viu router.
- `Assimalign.Cohesion.Viu.Server` — Cohesion Web middleware for static-web-assets manifests, SPA
  fallback, browser refresh, MIME mapping, cross-origin isolation, and Viu server rendering.
- `Assimalign.Cohesion.Viu.DevServer` — a packaged Cohesion development server and the MSBuild
  target that selects it from the Viu Browser SDK.
- `Assimalign.Cohesion.Viu.Templates` — the `viu-cohesion` and `viu-cohesion-web` templates.
- `Assimalign.Cohesion.Viu.Markdown` — generated Markdown catalogs, content sources, router-aware
  node rendering, page and navigation components, and optional hosting registration.
- `Assimalign.Cohesion.Viu.ApplicationModel` — typed site validation and descriptors layered over the Web planner.

All eight packages are versioned together. The current platform version is `10.0.0-preview.1`; it
composes the Cohesion and Viu builds [`global.json`](global.json) pins (see
[Getting the Assimalign.Cohesion and Assimalign.Viu packages](#getting-the-assimaligncohesion-and-assimalignviu-packages)).

## Application hosting

In an `Assimalign.Viu.Sdk.Browser` application, select the browser runtime explicitly before
building. The project must set `<EnablePreviewFeatures>true</EnablePreviewFeatures>` while the
Cohesion 10.0 preview hosting surface remains preview-annotated:

```csharp
using Assimalign.Cohesion.Viu.Hosting;
using Assimalign.Cohesion.Viu.Hosting.Browser;
using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Viu;
using Assimalign.Viu.Components;

ViuApplicationBuilder builder = ViuApplication.CreateBuilder();

builder.Configuration["features:greeting"] = "enabled";
builder.Services.AddSingleton<IGreetingService>(
    _ => new GreetingService());
builder.Services.AddComponents();

builder.UseRootComponent<App>();
builder.ConfigureBrowser();

ViuApplication application = builder.Build();
await application.RunAsync();
```

Components are contributed through `builder.Services`; `Build()` composes every contribution into
the application's component factory, independent of `ConfigureApplication` order. Unset state and
directive resolvers come from services, and unset warning and error handlers write to standard
error. Routing is registered the same way through `Assimalign.Cohesion.Viu.Routing`:
`builder.Services.AddRouting(...)` before building and `application.UseRouter()` after.

Register services as instances or explicit factories, keeping activation AOT/trimming safe.
Generated catalogs publish direct delegates at compile time; no runtime assembly scanning or
reflection activation is required. The built provider is supplied as Viu's `context.Services`;
Cohesion configuration is registered in that provider.

`Assimalign.Cohesion.Viu.Hosting` owns the provider and configuration manager it builds. User
services start before the selected Viu runtime and stop after it. Browser cancellation follows the
public Viu lifetime; Viu does not yet expose a browser-unload notification hook.

## Markdown content

Reference `Assimalign.Cohesion.Viu.Markdown` version `10.0.0-preview.1`. Its incremental generator reads
`docs/**/*.md` by default and emits `GeneratedMarkdownContent.Catalog` in the application's root
namespace. Titles, descriptions, status, and navigation order come from the Markdown files; there
is no frontmatter or handwritten page table. Keep the Markdown files linked as static web assets
when using HTTP content.

```csharp
builder.AddMarkdownContent(options =>
{
    options.Catalog = GeneratedMarkdownContent.Catalog;
    options.Source = new HttpMarkdownContentSource(httpClient);
});

var routes = MarkdownRoutes.Create(GeneratedMarkdownContent.Catalog,
    new MarkdownRouteOptions
    {
        LayoutComponent = ComponentReference.ForName("AppShell"),
    });
```

Register the router with `builder.Services.AddRouting()` and `builder.Services.AddRoutes(_ => routes)`
from `Assimalign.Cohesion.Viu.Routing`, then call `application.UseRouter()` after building. The
shell supplies `depth = 1` to its nested RouterView.
`MarkdownPageView` renders immutable Viu nodes, including `RouterLink` components for known relative
Markdown links. No anchor-click script is needed. Web history supports ordinary in-page fragments;
the host owns its scroll policy. A file-system source and awaited server prefetch provide the SSR
seam without depending on the Server package. See the [package overview](platforms/cohesion/Assimalign.Cohesion.Viu.Markdown/docs/OVERVIEW.md)
for project settings and plain service-provider composition.

## Cohesion Web and server rendering

Compose server rendering in the Web host's single container:

```csharp
using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Cohesion.Viu.Server;
using Assimalign.Cohesion.Web.Hosting;
using Assimalign.Viu.Components;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddSingleton<ITaskCatalog>(catalog)
    .AddViuServerRendering(rendering =>
    {
        rendering.RootComponent = ComponentNode.For<ServerReport>();
        rendering.DocumentShell = _ => ReportDocumentShell.Instance;
    })
    .AddViuClient();

await using WebApplication application = builder.Build();
application.UseViuServerRenderer(context => context.Request.Path.Value == "/report");
application.UseViuApplication();
await application.RunAsync();
```

`rendering.UseRootComponent<ServerReport>()` is equivalent to assigning the typed node.
Generated component and render aggregates include application and referenced Viu assemblies.
A packaged compiler bridge publishes referenced catalogs for Cohesion-only Web executables.
`Components` and `ServerRenders` replace those defaults independently. The render registry freezes
at Web build; root, shell factory, state, directives, and diagnostics are captured then. Repeated
`AddViuServerRendering` calls configure one composition. Unset warning/error handlers write to
standard error. Component services come from the Web container through `Context.Services`.

The Web host starts, stops, and disposes the composition runtime. No separate Viu host or provider
is built. Every accepted render owns a fresh `ApplicationContext`, `ServerRenderApplication`,
`SsrContext`, and asynchronous Cohesion DI scope. Scoped services and unset state/directive
resolvers use that scope; singleton services remain shared. Viu tears down components before the
scope releases its resources, including after failure or cancellation. Explicit state/directive
instances are borrowed and shared. Cohesion's `IHttpContext` has no request-provider seam, so the
scope belongs to this render, is not shared with other Web middleware, and does not inject HTTP
context into services. The document-shell factory receives HTTP context directly.

The earlier `ConfigureServerRendering`, `AddViuServerApplication(ViuApplication)`, and
`UseViuServerRenderer(ViuApplication, ServerRenderRegistry, ...)` overloads remain available for
caller-owned composition. They preserve their borrowed provider and caller-owned disposal behavior.
The explicit `ServerRenderAdaptor<IHttpContext>` overloads remain the advanced seam for custom
request roots, service providers, and scopes. Custom pipeline implementations supply an adaptor;
the predicate-only overload requires a built Cohesion `WebApplication`.

All renderer overloads produce HTML with `Cache-Control: no-store`, propagate request cancellation,
and pass through non-GET requests, reserved resource paths, and requests rejected by the predicate.
The document shell preserves `prefix -> progressive render -> teleport suffix` order. Destinations
belong in the suffix; already emitted head/body content needs buffering or a prepass to change.
Cohesion currently buffers responses, so a render/adaptor failure before transport commitment can
clear the body, set 500, and propagate to the host exception boundary. A component error handled by
the configured Viu error handler follows Viu's normal handled-error semantics.

See the [Server overview](platforms/cohesion/Assimalign.Cohesion.Viu.Server/docs/OVERVIEW.md).

## Development server

A browser application references both the browser hosting package and the development-server
package:

```xml
<PackageReference Include="Assimalign.Cohesion.Viu.Hosting.Browser"
                  Version="10.0.0-preview.1" />
<PackageReference Include="Assimalign.Cohesion.Viu.DevServer"
                  Version="10.0.0-preview.1" />
```

The DevServer target selects the Viu Browser SDK's custom-host contract, runs the packaged
`Assimalign.Cohesion.Viu.DevServer.dll`, supplies the build output and both static-web-assets
manifests, and waits for `Now listening on:`. An explicit `ViuBrowserDevServerCommand` continues to
take precedence. The executable may also be run directly:

```powershell
dotnet exec Assimalign.Cohesion.Viu.DevServer.dll `
  --content-root C:\path\to\bin\Debug\net10.0\wwwroot `
  --endpoints-manifest C:\path\to\App.staticwebassets.endpoints.json `
  --runtime-manifest C:\path\to\App.staticwebassets.runtime.json `
  --urls http://127.0.0.1:5080
```

`ASPNETCORE_URLS` overrides `--urls`; without either setting the server reserves a loopback port.
Pass `--apply-cop-headers` for WebAssembly threads.

## Template

After packing the local family, install and generate the development template:

```powershell
dotnet new install ./_out/packages/Assimalign.Cohesion.Viu.Templates.10.0.0-preview.1.nupkg
dotnet new viu-cohesion --name HelloViu --assimalignRoot C:/Source/repos/assimalign
cd HelloViu
dotnet watch
```

The generated application uses `ViuApplication.CreateBuilder()`, `ConfigureBrowser()`, typed roots, generated component aggregates, typed routes, and the Cohesion development server.

## Local build

`NuGet.config` restores Cohesion and Viu from the Assimalign GitHub Packages feed and this
repository's own packages from `_out/packages`. Package
versions are applied with `PackageReference Update` items in `Directory.Build.targets`; this
repository does not use NuGet central package management.

```powershell
pwsh scripts/Install-Local.ps1 -Configuration Release
```

With the sibling `viu-docs` checkout present, verify the packed Markdown generator against its
preserved 45-page route/asset baseline:

```powershell
pwsh scripts/Verify-MarkdownDocs.ps1
```

`Install-Local.ps1` restores, builds, tests, and packs all eight packages into `_out/packages`. Shipping
libraries target `net10.0`, use explicit registration, and enable trimming/AOT compatibility
analysis. The Server package carries Cohesion's App.Web framework reference; Hosting.Browser has
only its host-neutral Hosting dependency and the Viu Browser runtime dependency.

## Getting the Assimalign.Cohesion and Assimalign.Viu packages

Cohesion comes from the Assimalign GitHub Packages feed, never from a sibling checkout. The one pin
is the `Assimalign.Cohesion.Sdk` entry in [`global.json`](global.json)'s `msbuild-sdks` block:
both Cohesion SDK entries must carry the same version (the Cohesion SDK enforces this as
COHSDK002), and [`Build.References.Packages.targets`](build/Targets/Build.References.Packages.targets)
reads that entry, so every Cohesion library reference restores exactly the same build as the SDK
and framework packs. To move to another Cohesion build, edit the two `global.json` entries and
nothing else:

- `10.0.0-preview.0.ci.<run>` — a build of cohesion `main`, published by cohesion's
  `ci-packages.yml` for every merge. The current pin is `10.0.0-preview.0.ci.21` (cohesion
  `4c52f8c`).
- `10.0.0-preview.0.pr.<pr>.<run>` — a build of an open cohesion pull request, for integrating
  unreleased Cohesion work without packing it locally.
- `10.0.0-preview.1` and later — releases.

A CI pin is temporary: cohesion deletes a pull request's builds when it closes and keeps only the
newest 20 `main` builds, so move the pin at least every few days and pin a release before tagging a
release here. GitHub Packages requires a token with `read:packages` even for these public packages.
Never write it to `NuGet.config`; supply it from the environment:

```powershell
$env:NuGetPackageSourceCredentials_assimalign = "Username=<github-user>;Password=$(gh auth token);ValidAuthenticationTypes=Basic"
```

CI sets the same variable from the workflow token. The isolated package cache is
`.nuget/packages`.

Viu comes from the same feed the same way. The one pin is the `Assimalign.Viu.Sdk` entry in
`global.json`; keep `Assimalign.Viu.Sdk.Browser` at the same version, and
`Build.References.Packages.targets` reads the `Assimalign.Viu.Sdk` entry for every Viu library
reference. viu's `ci-packages.yml` publishes `10.0.0-preview.0.ci.<run>` for every merge to viu
`main` and `10.0.0-preview.0.pr.<pr>.<run>` for an open viu pull request, under the same retention
rules. The current pin is `10.0.0-preview.0.pr.379.5` (Viu PR #379). A CI build of this repository
carries both pins into its package dependencies, so a consumer restores the whole set from the feed.

## CI packages

[`.github/workflows/ci-packages.yml`](.github/workflows/ci-packages.yml) publishes all eight
packages to the Assimalign GitHub Packages feed for every push to `main` and every commit to an open
same-repository pull request, so consumers such as `viu-examples` can pin a build instead of packing
this repository. The model is cohesion's:

- **Version shape.** `Get-ViuPlatformsCiPackageVersion` in
  [`ViuPlatformsPackaging.psm1`](scripts/modules/ViuPlatformsPackaging.psm1) extends the
  predecessor of `ViuPlatformsVersion`, so every CI build sorts below the release it builds toward:
  `10.0.0-preview.1` gives `10.0.0-preview.0.ci.<run>` for `main` and
  `10.0.0-preview.0.pr.<pr>.<run>` for a pull request.
- **What ships.** [`scripts/Pack-Release.ps1`](scripts/Pack-Release.ps1) packs exactly the
  inventory in `ViuPlatformsPackaging.psm1`, the same one `release.yml` and `Install-Local.ps1`
  use, and the solution's tests run against that build before anything is pushed. A CI build
  carries the Cohesion and Viu pins of its commit into its package dependencies.
- **Retention.** [`.github/workflows/ci-packages-prune.yml`](.github/workflows/ci-packages-prune.yml)
  deletes a pull request's builds when it closes and every `main` build outside the newest 20,
  and nothing else; a release version is never selected.
  [`scripts/tests/Test-CiPackages.ps1`](scripts/tests/Test-CiPackages.ps1) pins both rules.
- **Permissions.** Deleting through `GITHUB_TOKEN` needs the Admin role on each package, which
  GitHub grants to the repository whose workflow published it. If deletions are refused (HTTP
  403), give `assimalign/viu-platforms` the **Admin** role under the package's **Manage Actions
  access**. `workflow_dispatch` on the prune workflow defaults to a dry run.

## A deployable Viu Web site

```pwsh
dotnet new install ./_out/packages/Assimalign.Cohesion.Viu.Templates.10.0.0-preview.1.nupkg
dotnet new viu-cohesion-web --name Acme.Docs --assimalignRoot C:/Source/repos/assimalign
cd Acme.Docs
dotnet build
dotnet run --project Acme.Docs.Web
dotnet run --project Acme.Docs.AppHost -- --gateway local --mode describe
```

The template creates Client, Web and AppHost projects. Web is an enabled Web resource with a
public HTTP endpoint. Declare `<ProjectReference Include="../Client/Client.csproj" ViuClient="true" />` on Web.
It participates in normal restore/build order, automatically sets `ReferenceOutputAssembly=false`
and `Private=false`, and queries the Browser SDK's `GetViuBrowserClientInfo` target.
`builder.Services.AddViuClient()` (or `options.UseClientOutput()` inside an explicit configuration
callback) infers the single client and selects build manifests during
development, through the `viu-client.json` document the build stages beside the executable and in
an in-process member's resource content root, and a physical `wwwroot` beside the executable after
publication. The bare health
probes and the resource control-plane path (`ViuApplicationOptions.ControlPlanePath`, default
`/cohesion/v1`, plus its subtree) always pass through Viu's static and server-rendering fallbacks;
every other route, including an application route such as `/cohesion`, belongs to the browser
application.

The server serves a source-generated `cohesion/viu-client/v1` document at `/_cohesion/client.json`
with `Cache-Control: no-store`. It includes only application/resource/environment/gateway names
and referenced endpoint URLs. `ClientConfigurationPath=null` disables it. Browser
`AddClientConfiguration(httpClient)` loads it before Viu starts and registers a synchronous
`ViuClientConfigurationProvider`; failed requests warn and retain `ViuClientConfiguration.Empty`.

```pwsh
dotnet publish Acme.Docs.Web -c Release -r linux-arm64
dotnet msbuild Acme.Docs.Web/Acme.Docs.Web.csproj -t:CohesionPublishImage -p:Configuration=Release -p:RuntimeIdentifier=linux-x64 -p:CohesionContainerRepository=docs/site -p:CohesionContainerPush=false
```

Publishing Web publishes the client in Release with the browser's own RID and trimming settings,
then includes its complete `wwwroot` in Web's publish output. Cohesion's image target creates a
self-contained OCI archive without a Dockerfile. It needs the Cohesion App/App.Web runtime packs
for the chosen RID. The current Cohesion image resolver accepts only `linux-x64` and
`linux-musl-x64`; ordinary `linux-arm64` publish works, but the image target needs an upstream
ARM64 extension before it can target the local Podman machine. Release images require NativeAOT
and a supported Linux build host/toolchain; Debug permits self-contained JIT images.

`Assimalign.Cohesion.Viu.ApplicationModel` adds `AddViuWeb` validation and typed metadata, while
using exactly the `AddWeb` plan. Generated `AddAllResources()` still calls `AddWeb` until Cohesion's
`CohesionGatewayResourceKind` selection supports the overlay. Keep the Web executable's manifest
application model as `Assimalign.Cohesion.Web.ApplicationModel`. See the
[ApplicationModel overview](platforms/cohesion/Assimalign.Cohesion.Viu.ApplicationModel/docs/OVERVIEW.md).

The pre-release `ViuClientReference` item is removed; builds print the new marked
`ProjectReference` form. Explicit `UseClientOutput("Client.Assembly")` remains available.

After packing, run `pwsh scripts/tests/Test-Templates.ps1` for isolated template builds,
Web publication with a trimmed client, and a packed SSR consumer using a referenced Viu catalog.
The lane keeps its hive, cache and generated projects under `_out` and treats warnings as errors.
