# Cohesion Viu project templates

Version `10.0.0-preview.1` contains two templates:

- `viu-cohesion`: one browser project with the Cohesion development server.
- `viu-cohesion-web`: Client, Web, and Gateway projects; Web is a plain Cohesion Web application
  that makes itself a managed resource, and Gateway runs it on the in-process platform.

Cohesion and Viu restore from the authenticated Assimalign GitHub Packages feed; platform
packages use the local feed. Install the
packed template and pass the directory containing the four sibling checkouts when needed:

```pwsh
dotnet new install ./_out/packages/Assimalign.Cohesion.Viu.Templates.10.0.0-preview.1.nupkg
dotnet new viu-cohesion-web --name Acme.Docs --assimalignRoot C:/Source/repos/assimalign
dotnet new viu-cohesion --name HelloViu --assimalignRoot C:/Source/repos/assimalign
```

Each generated README describes its build/run loop. SDK pins use Cohesion
`10.0.0-preview.0.pr.1759.172` and Viu `10.0.0-preview.2`. Supply feed credentials through
`NuGetPackageSourceCredentials_assimalign`. Components use generated aggregates and typed roots/
routes; Web uses `ProjectReference ViuClient="true"` and `services.AddViuClient()`. Another platform,
such as the Kubernetes pair from `cohesion-platforms`, changes both projects: Web opts into the
platform's instance package in `Program.cs`, and Gateway uses that platform's infrastructure instead
of `InProcessInfrastructure` and its reference to Web.
