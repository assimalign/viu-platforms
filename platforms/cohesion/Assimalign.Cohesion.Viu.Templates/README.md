# Cohesion Viu project templates

Version `10.0.0-beta.5` contains two templates:

- `viu-cohesion`: one browser project with the Cohesion development server.
- `viu-cohesion-web`: Client, enabled Web resource, and Local/Kubernetes AppHost projects.

This version tracks unreleased Cohesion and Viu lines through the sibling local feeds. Install the
packed template and pass the directory containing the four sibling checkouts when needed:

```pwsh
dotnet new install ./_out/packages/Assimalign.Cohesion.Viu.Templates.10.0.0-beta.5.nupkg
dotnet new viu-cohesion-web --name Acme.Docs --assimalignRoot C:/Source/repos/assimalign
dotnet new viu-cohesion --name HelloViu --assimalignRoot C:/Source/repos/assimalign
```

Each generated README describes its build/run loop. `NuGet.config` maps Assimalign packages to the
local feeds and uses nuget.org for public dependencies. SDK pins use Cohesion
`10.0.1-preview.3.local` and Viu `10.0.0-beta.12`. Package publishing to a remote feed is a separate
release step; these local packages should not be treated as immutable published artifacts.
