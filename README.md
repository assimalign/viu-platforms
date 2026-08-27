# Viu platform integrations

This repository connects Viu applications to host platforms without adding host-specific dependencies to the Viu runtime. The first integration targets Cohesion Web: a feature library serves Viu browser assets and adapts server rendering, a packaged development server replaces the default WebAssembly application host, and a `dotnet new` template wires the development loop.

## Layout

- `platforms/cohesion/Assimalign.Viu.Cohesion.Web` — Cohesion Web middleware for static-web-assets manifests, SPA fallback, browser refresh, and the Viu server-renderer seam.
- `platforms/cohesion/Assimalign.Viu.Cohesion.DevServer` — Cohesion-hosted command-line development server and its consumer MSBuild targets.
- `platforms/cohesion/Assimalign.Viu.Cohesion.Templates` — the `viu-cohesion` project template package.
- `platforms/aspnetcore` — reserved and intentionally empty.

The central build is deliberately smaller than Viu's in-repository build. Dependencies use ordinary `PackageReference` and `ProjectReference` items with NuGet Central Package Management; this repository does not reproduce Viu's name-resolved `ViuProjectReference` machinery.

## Local package feeds

`NuGet.config` uses the checked-out Viu and Cohesion package outputs:

- `C:\Source\repos\assimalign\viu\_out\packages`
- `C:\Source\repos\assimalign\cohesion\_out\packages`
- this repository's `_out\packages`
- nuget.org for test infrastructure

The bridge pins Viu ServerRenderer at `10.0.0-beta.11` and the Browser SDK contract at
`10.0.0-beta.10`, Cohesion's packaged HTTP/Web/Server surface at `10.0.2`, and the separately
versioned Cohesion file-system family at `10.0.2-preview.1`. Repack the reference repositories
before building here when those package APIs change.

The development server uses plain `Microsoft.NET.Sdk` plus
`Assimalign.Cohesion.Web.Server` 10.0.2. The packaged-consumer path through
`Assimalign.Cohesion.Sdk.Web` was tested first, but the matching `App.Web.Ref` and
`App.Web.Runtime.win-arm64` 10.0.1-preview.1 packs in the local feed contain only the area-root
assemblies; they do not contain Web Hosting, StaticFiles, or Testing. The server therefore owns a
tracked listener loop over the public packaged transport API. Its framework-dependent, no-RID
publish copies all Cohesion assemblies into the NuGet `tools/` payload and depends only on an
installed Microsoft.NETCore.App 10 runtime.

## Local development loop

Pack and install the template, then create a sample:

```powershell
pwsh scripts/Install-Local.ps1
dotnet new install Assimalign.Viu.Cohesion.Templates `
  --add-source C:\Source\repos\assimalign\viu-platforms\_out\packages
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
AOT compatibility analysis. Cohesion 10.0.2 marks its complete HTTP/Web surface with
`RequiresPreviewFeatures`; this repository must therefore opt in at the assembly level even though
the bridge itself avoids reflection, dynamic code, and runtime manifest discovery.
