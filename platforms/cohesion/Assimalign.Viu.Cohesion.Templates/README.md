# Viu Cohesion project templates

`Assimalign.Viu.Cohesion.Templates` contains the `viu-cohesion` template for a Viu Browser
WebAssembly application hosted during development by Cohesion Web.

Install the local package and create an application:

```powershell
dotnet new install Assimalign.Viu.Cohesion.Templates --add-source C:\Source\repos\assimalign\viu-platforms\_out\packages
dotnet new viu-cohesion --name HelloViu
```

The generated `NuGet.config` contains local-feed paths. The defaults match the integration
workspace used to build this package. Supply different paths when creating the project when the
repositories or package cache live elsewhere:

```powershell
dotnet new viu-cohesion --name HelloViu `
  --viuFeed C:\packages\viu `
  --cohesionFeed C:\packages\cohesion `
  --platformsFeed C:\packages\viu-platforms
```

From the generated directory, run `dotnet watch`. The Viu Browser SDK delegates development
hosting to `Assimalign.Viu.Cohesion.DevServer`, whose package build target supplies the command
and readiness prefix expected by the SDK.
