# Assimalign.Cohesion.Viu.Templates overview

`Assimalign.Cohesion.Viu.Templates` is a content-only `dotnet new` package. It provides two templates. The development
template, `viu-cohesion`, creates a Viu Browser WebAssembly application and selects the
Cohesion development server through a package reference.

The generated project contains:

- a Viu single-file counter component;
- a Cohesion-shaped Viu application entry point using explicit browser-host registration, generated aggregates and typed roots/routes;
- the HTML and JavaScript boot assets required by the Viu Browser SDK;
- references to `Assimalign.Cohesion.Viu.Hosting.Browser` and
  `Assimalign.Cohesion.Viu.DevServer` `10.0.0-preview.1`; and
- a `NuGet.config` mapping the configurable sibling local platform feeds, GitHub Packages and nuget.org.

The template pins `Assimalign.Viu.Sdk` and `Assimalign.Viu.Sdk.Browser` to
`10.0.0-preview.0.pr.379.5` in `global.json`. This keeps project evaluation deterministic before NuGet can
restore the SDK packages.

## Deployable Web resource template

Version `10.0.0-preview.1` adds `viu-cohesion-web`, producing Client, Web, AppHost and a solution.
Pass `--assimalignRoot` when the output is outside the sibling-checkout directory. Cohesion SDKs default to `10.0.0-preview.0.ci.21`; Viu uses PR #379 build 5.
Web declares its client with a normal `ProjectReference` marked `ViuClient="true"`, and AppHost defaults to Local; Kubernetes requires matching gateway packages. See the
[generated README](../content/viu-cohesion-web/README.md) for build, run and container commands.

Both templates request generated aggregates through `AddComponents()`, choose typed roots with
`UseRootComponent<App>()` and add typed Counter routes through `routing.Routes.Add<Counter>`.
Web registers `services.AddViuClient()` and infers its sole marked client. Project references
participate in restore before build; Server metadata suppresses compile references. Explicit
`ViuClientReference` items are retired. Server rendering can be added with
`services.AddViuServerRendering` and the predicate-only `UseViuServerRenderer` overload.

After packing, run `pwsh scripts/tests/Test-Templates.ps1` for isolated template builds,
Web publication with a trimmed client, and a packed SSR consumer using a referenced Viu catalog.
The lane keeps its hive, cache and generated projects under `_out` and treats warnings as errors.
