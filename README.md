# Viu platform integrations

This repository packages host-platform integrations for Viu. The Cohesion family adds an
optional Cohesion application host around Viu, a browser host, Cohesion Web asset and
server-rendering middleware, Markdown content, a development server, and a `dotnet new` template. Ordinary Viu
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
- `Assimalign.Cohesion.Viu.Templates` — the `viu-cohesion` project template.
- `Assimalign.Cohesion.Viu.Markdown` — generated Markdown catalogs, content sources, router-aware
  node rendering, page and navigation components, and optional hosting registration.

All six packages are versioned together. The current platform version is `10.0.0-beta.3`; it
composes Cohesion `10.0.0-beta.1` and Viu `10.0.0-beta.11`.

## Application model

In an `Assimalign.Viu.Sdk.Browser` application, select the browser runtime explicitly before
building. The project must set `<EnablePreviewFeatures>true</EnablePreviewFeatures>` while the
Cohesion `10.0.0-beta.1` hosting surface remains preview-annotated:

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
public Viu lifetime; Viu beta.11 does not yet expose a browser-unload notification hook.

## Markdown content

Reference `Assimalign.Cohesion.Viu.Markdown` version `10.0.0-beta.3`. Its incremental generator reads
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
                  Version="10.0.0-beta.3" />
<PackageReference Include="Assimalign.Cohesion.Viu.DevServer"
                  Version="10.0.0-beta.3" />
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

After configuring credentials for the Assimalign GitHub Packages feed, install and generate the
template:

```powershell
dotnet new install Assimalign.Cohesion.Viu.Templates::10.0.0-beta.3
dotnet new viu-cohesion --name HelloViu
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

The script restores, builds, tests, and packs all six packages into `_out/packages`. Shipping
libraries target `net10.0`, use explicit registration, and enable trimming/AOT compatibility
analysis. The Server package carries Cohesion's App.Web framework reference; Hosting.Browser has
only its host-neutral Hosting dependency and the Viu Browser runtime dependency.
