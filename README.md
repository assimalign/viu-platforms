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
- `Assimalign.Cohesion.Viu.Server` — Cohesion Web middleware for static-web-assets manifests, SPA
  fallback, browser refresh, MIME mapping, cross-origin isolation, and Viu server rendering.
- `Assimalign.Cohesion.Viu.DevServer` — a packaged Cohesion development server and the MSBuild
  target that selects it from the Viu Browser SDK.
- `Assimalign.Cohesion.Viu.Templates` — the `viu-cohesion` and `viu-cohesion-web` templates.
- `Assimalign.Cohesion.Viu.Markdown` — generated Markdown catalogs, content sources, router-aware
  node rendering, page and navigation components, and optional hosting registration.
- `Assimalign.Cohesion.Viu.ApplicationModel` — typed site validation and descriptors layered over the Web planner.

All seven packages are versioned together. The current platform version is `10.0.0-preview.1`; it
composes Cohesion `10.0.0-preview.1.local` and Viu `10.0.0-preview.1`.

## Application hosting

In an `Assimalign.Viu.Sdk.Browser` application, select the browser runtime explicitly before
building. The project must set `<EnablePreviewFeatures>true</EnablePreviewFeatures>` while the
Cohesion `10.0.0-preview.1.local` hosting surface remains preview-annotated:

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

builder.ConfigureApplication(options =>
{
    ComponentFactory components = new();
    GeneratedViuComponents.Register(components);

    options.Components = components;
    options.RootComponent = new ComponentNode(
        ComponentReference.ForName("App"));
});
builder.ConfigureBrowser();

ViuApplication application = builder.Build();
await application.RunAsync();
```

Service registrations on this WebAssembly path must use an existing instance or an explicit
factory. Type-based activation, assembly scanning, configuration binding, and other
reflection-driven composition are intentionally rejected or excluded. The built provider is
supplied as Viu's `context.Services`; Cohesion configuration is registered in that provider.

`Assimalign.Cohesion.Viu.Hosting` owns the provider and configuration manager it builds. User
services start before the selected Viu runtime and stop after it. Browser cancellation follows the
public Viu lifetime; Viu beta.12 does not yet expose a browser-unload notification hook.

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

Compose the router over `routes` in application services. The shell supplies `depth = 1` to its nested RouterView.
`MarkdownPageView` renders immutable Viu nodes, including `RouterLink` components for known relative
Markdown links. No anchor-click script is needed. Web history supports ordinary in-page fragments;
the host owns its scroll policy. A file-system source and awaited server prefetch provide the SSR
seam without depending on the Server package. See the [package overview](platforms/cohesion/Assimalign.Cohesion.Viu.Markdown/docs/OVERVIEW.md)
for project settings and plain service-provider composition.

## Cohesion Web and server rendering

`Assimalign.Cohesion.Viu.Server` preserves the existing `AddViuApplication`,
`UseViuApplication`, and adaptor-based `UseViuServerRenderer` APIs. Its hosting-model overloads
borrow the frozen composition from a `ViuApplication`, freeze the explicit server-render registry,
and create a fresh `ServerRenderApplication` and `SsrContext` for every request. A Cohesion Web
builder registration can link the borrowed server application's start/stop lifecycle to the Web
host.

The long-lived server application owns composition and middleware lifetime only; it never reuses a
server-render application across requests. The convenience path shares the application provider
and state. Applications that require a request-specific service provider should keep using the
advanced `ServerRenderAdaptor<IHttpContext>` overload and provide their own request-scope factory.

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

The generated application uses `ViuApplication.CreateBuilder()`, `ConfigureBrowser()`, explicit
generated-component registration, and the Cohesion development server.

## Local build

`NuGet.config` restores this repository's packages plus the sibling Cohesion and Viu package
outputs. Package versions are applied with `PackageReference Update` items in
`Directory.Build.targets`; this repository does not use NuGet central package management.

```powershell
pwsh scripts/Install-Local.ps1 -Configuration Release
```

With the sibling `viu-docs` checkout present, verify the packed Markdown generator against its
preserved 45-page route/asset baseline:

```powershell
pwsh scripts/Verify-MarkdownDocs.ps1
```

The script restores, builds, tests, and packs all seven packages into `_out/packages`. Shipping
libraries target `net10.0`, use explicit registration, and enable trimming/AOT compatibility
analysis. The Server package carries Cohesion's App.Web framework reference; Hosting.Browser has
only its host-neutral Hosting dependency and the Viu Browser runtime dependency.

## Getting the Assimalign.Cohesion packages

This tree tracks unreleased Cohesion `10.0.0-preview.1` and Viu `10.0.0-preview.1` sources. Published
package identities are immutable. With `../cohesion/_out/packages` present, ordinary Cohesion
references default to exactly `10.0.0-preview.1.local`; override with
`-p:CohesionSiblingPackageVersion=<identity>`. Without it they use the release floor
`[10.0.0-preview.1, )`. The project SDK pins in `global.json` and DevServer also use `.local`:
MSBuild SDK resolution reads `NuGet.config` before restore properties. A complete local feed is
required; the partial canonical SDK/framework feed cannot replace the `.local` library closure.
The isolated cache is `.nuget/packages`.

Use the prepacked sibling feeds in `cohesion`, `viu`, and `cohesion-platforms`; the latter supplies
Kubernetes/Docker/Containers at `10.0.0-preview.1`. Run this repository's `Install-Local.ps1` to pack
all seven hosting-family packages at `10.0.0-preview.1`. CI recreates Cohesion and Viu sibling feeds
from `COHESION_SIBLING_REF` and `VIU_SIBLING_REF` repository variables (prefer immutable commits).
It replaces the root NuGet config so SDK and package resolution use those same feeds. Viu's
beta.12 Browser SDK was absent from GitHub Packages when this integration was prepared.

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
public HTTP endpoint. Declare a `ViuClientReference` item on a Web executable referencing Server;
the package turns it into a non-compiling client project reference and queries the client's
evaluated target path. `AddViuApplication(o => o.UseClientOutput("Acme.Docs.Client"))` selects build manifests during
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
