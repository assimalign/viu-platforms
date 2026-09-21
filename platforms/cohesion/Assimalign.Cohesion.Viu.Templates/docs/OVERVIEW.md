# Assimalign.Cohesion.Viu.Templates overview

`Assimalign.Cohesion.Viu.Templates` is a content-only `dotnet new` package. It provides two templates. The development
template, `viu-cohesion`, creates a Viu Browser WebAssembly application and selects the
Cohesion development server through a package reference.

The generated project contains:

- a Viu single-file counter component;
- a Cohesion-shaped Viu application entry point using explicit browser-host and generated-component registration;
- the HTML and JavaScript boot assets required by the Viu Browser SDK;
- references to `Assimalign.Cohesion.Viu.Hosting.Browser` and
  `Assimalign.Cohesion.Viu.DevServer` `10.0.0-preview.1`; and
- a `NuGet.config` mapping the configurable sibling local feeds plus nuget.org.

The template pins `Assimalign.Viu.Sdk` and `Assimalign.Viu.Sdk.Browser` to
`10.0.0-preview.1` in `global.json`. This keeps project evaluation deterministic before NuGet can
restore the SDK packages.

## Deployable Web resource template

Version `10.0.0-preview.1` adds `viu-cohesion-web`, producing Client, Web, AppHost and a solution.
Pass `--assimalignRoot` when the output is outside the sibling-checkout directory. SDK and library
Cohesion identities are aligned at `10.0.0-preview.1.local`; Viu stays at `10.0.0-preview.1`.
Web references Server with `ViuClientProject`, and AppHost selects Local or Kubernetes. See the
[generated README](../content/viu-cohesion-web/README.md) for build, run and container commands.
