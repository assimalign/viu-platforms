# Viu platform integrations

This repository connects Viu applications to host platforms without adding host-specific dependencies to the Viu runtime. The first integration targets Cohesion Web: a feature library serves Viu browser assets and adapts server rendering, a packaged development server replaces the default WebAssembly application host, and a `dotnet new` template wires the development loop.

## Layout

- `platforms/cohesion/Assimalign.Viu.Cohesion.Web` — Cohesion Web middleware for static-web-assets manifests, SPA fallback, browser refresh, and the Viu server-renderer seam.
- `platforms/cohesion/Assimalign.Viu.Cohesion.DevServer` — Cohesion-hosted command-line development server and its consumer MSBuild targets.
- `platforms/cohesion/Assimalign.Viu.Cohesion.Templates` — the `viu-cohesion` project template package.
- `platforms/aspnetcore` — reserved and intentionally empty.

The central build is deliberately smaller than Viu's in-repository build. Package dependencies use
NuGet Central Package Management, project dependencies use ordinary `ProjectReference` items, and
Cohesion Web is consumed through its SDK/shared-framework model; this repository does not reproduce
Viu's name-resolved `ViuProjectReference` machinery.

## Local package feeds

`NuGet.config` uses the checked-out Viu and Cohesion package outputs:

- `C:\Source\repos\assimalign\viu\_out\packages`
- `C:\Source\repos\assimalign\cohesion\_out\packages`
- this repository's `_out\packages`
- nuget.org for test infrastructure

The bridge pins Viu ServerRenderer at `10.0.0-beta.11`, the Browser SDK contract at
`10.0.0-beta.10`, and `Assimalign.Cohesion.Sdk.Web` plus the `Assimalign.Cohesion.App.Web` shared
framework at `10.0.0-beta.1`. Repack the reference repositories before building here when those
package APIs change.

Both the bridge library and development server use `Assimalign.Cohesion.Sdk.Web`. The bridge's
package records its App/App.Web framework references and its only ordinary dependency is Viu
ServerRenderer. The development server composes `WebApplication.CreateBuilder()`, then its package
publish copies the Cohesion runtime-pack assemblies into `tools/`; consumers need only an installed
Microsoft.NETCore.App 10 runtime and do not install Cohesion shared frameworks globally.

## Package consumption

Assimalign prerelease packages are published through the organization GitHub Packages feed. Set a
GitHub identity and a personal access token with `read:packages`, then install the template and
create a sample:

```powershell
$env:GITHUB_ACTOR = "your-github-login"
$env:GITHUB_TOKEN = "your-read-packages-pat"
dotnet nuget add source https://nuget.pkg.github.com/assimalign/index.json `
  --name github `
  --username $env:GITHUB_ACTOR `
  --password $env:GITHUB_TOKEN `
  --store-password-in-clear-text
dotnet new install Assimalign.Viu.Cohesion.Templates::10.0.0-beta.1
dotnet new viu-cohesion --name HelloViu
cd HelloViu
dotnet watch
```

The generated project references `Assimalign.Viu.Cohesion.DevServer`. Its NuGet build target selects
the Viu Browser SDK's custom-host contract, starts the packaged tool with the build output and both
static-web-assets manifests, and waits for the exact `Now listening on:` readiness prefix. Explicit
`ViuBrowserDevServerCommand` configuration wins over the package defaults.

The executable can also be started directly:

```powershell
dotnet exec Assimalign.Viu.Cohesion.DevServer.dll `
  --content-root C:\path\to\bin\Debug\net10.0\wwwroot `
  --endpoints-manifest C:\path\to\App.staticwebassets.endpoints.json `
  --runtime-manifest C:\path\to\App.staticwebassets.runtime.json `
  --urls http://127.0.0.1:5080
```

`ASPNETCORE_URLS` overrides `--urls`; without either, the server reserves a loopback port. Add
`--apply-cop-headers` for WebAssembly threads.

## Build and test

```powershell
dotnet restore Assimalign.Viu.Platforms.slnx
dotnet build Assimalign.Viu.Platforms.slnx --no-restore
dotnet test Assimalign.Viu.Platforms.slnx --no-build
pwsh scripts/Install-Local.ps1
```

Packages are written to `_out\packages`. The install script restores, builds, tests, publishes the development-server payload, and packs the three shipping projects without committing or installing anything globally.

All shipping projects use `net10.0`, preview C# syntax, explicit usings, nullable annotations, and
AOT compatibility analysis. Cohesion `10.0.0-beta.1` marks its complete HTTP/Web surface with
`RequiresPreviewFeatures`; this repository opts in at the assembly level while the bridge itself
continues to avoid reflection, dynamic code, and runtime manifest discovery.
