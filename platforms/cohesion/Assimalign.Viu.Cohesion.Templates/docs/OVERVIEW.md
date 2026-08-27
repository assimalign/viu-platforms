# Assimalign.Viu.Cohesion.Templates overview

`Assimalign.Viu.Cohesion.Templates` is a content-only `dotnet new` package. It provides one
template, `viu-cohesion`, which creates a Viu Browser WebAssembly application and selects the
Cohesion development server through a package reference.

The generated project contains:

- a Viu single-file counter component;
- a Browser application entry point using explicit generated-component registration;
- the HTML and JavaScript boot assets required by the Viu Browser SDK;
- a reference to `Assimalign.Viu.Cohesion.DevServer` `10.0.0-beta.1`; and
- a `NuGet.config` that uses the Assimalign GitHub Packages feed with environment-provided
  credentials plus nuget.org.

The template pins `Assimalign.Viu.Sdk` and `Assimalign.Viu.Sdk.Browser` to
`10.0.0-beta.10` in `global.json`. This keeps project evaluation deterministic before NuGet can
restore the SDK packages.
