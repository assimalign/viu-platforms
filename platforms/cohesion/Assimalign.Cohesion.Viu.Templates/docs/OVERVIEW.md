# Assimalign.Cohesion.Viu.Templates overview

`Assimalign.Cohesion.Viu.Templates` is a content-only `dotnet new` package. It provides one
template, `viu-cohesion`, which creates a Viu Browser WebAssembly application and selects the
Cohesion development server through a package reference.

The generated project contains:

- a Viu single-file counter component;
- a Cohesion-shaped Viu application entry point using explicit browser-host and generated-component registration;
- the HTML and JavaScript boot assets required by the Viu Browser SDK;
- references to `Assimalign.Cohesion.Viu.Hosting.Browser` and
  `Assimalign.Cohesion.Viu.DevServer` `10.0.0-beta.3`; and
- a `NuGet.config` that uses the Assimalign GitHub Packages feed with environment-provided
  credentials plus nuget.org.

The template pins `Assimalign.Viu.Sdk` and `Assimalign.Viu.Sdk.Browser` to
`10.0.0-beta.11` in `global.json`. This keeps project evaluation deterministic before NuGet can
restore the SDK packages.
