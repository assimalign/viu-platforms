# Viu platform integrations

This repository packages host-platform integrations for Viu. The Cohesion family adds an
optional Cohesion application host around Viu, a browser host, Cohesion Web asset and
server-rendering middleware, a development server, and a `dotnet new` template. Ordinary Viu
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

All five packages are versioned together. The current platform version is `10.0.0-beta.2`; it
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
                  Version="10.0.0-beta.2" />
<PackageReference Include="Assimalign.Cohesion.Viu.DevServer"
                  Version="10.0.0-beta.2" />
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
dotnet new install Assimalign.Cohesion.Viu.Templates::10.0.0-beta.2
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

The script restores, builds, tests, and packs all five packages into `_out/packages`. Shipping
libraries target `net10.0`, use explicit registration, and enable trimming/AOT compatibility
analysis. The Server package carries Cohesion's App.Web framework reference; Hosting.Browser has
only its host-neutral Hosting dependency and the Viu Browser runtime dependency.
