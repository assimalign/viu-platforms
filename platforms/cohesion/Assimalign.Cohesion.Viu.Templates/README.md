# Cohesion Viu project templates

Version `10.0.0-preview.1` contains two templates:

- `viu-cohesion`: one browser project with the Cohesion development server.
- `viu-cohesion-web`: Client, enabled Web resource, and Local AppHost projects.

Cohesion and Viu restore from the authenticated Assimalign GitHub Packages feed; platform
packages use the local feed. Install the
packed template and pass the directory containing the four sibling checkouts when needed:

```pwsh
dotnet new install ./_out/packages/Assimalign.Cohesion.Viu.Templates.10.0.0-preview.1.nupkg
dotnet new viu-cohesion-web --name Acme.Docs --assimalignRoot C:/Source/repos/assimalign
dotnet new viu-cohesion --name HelloViu --assimalignRoot C:/Source/repos/assimalign
```

Each generated README describes its build/run loop. SDK pins use Cohesion
`10.0.0-preview.0.ci.21` and Viu `10.0.0-preview.0.pr.379.5`. Supply feed credentials through
`NuGetPackageSourceCredentials_assimalign`. Components use generated aggregates and typed roots/
routes; Web uses `ProjectReference ViuClient="true"` and `services.AddViuClient()`. Local is the
default gateway; Kubernetes requires explicit opt-in and gateway packages built against the same
Cohesion pin.
