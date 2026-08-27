# Viu Cohesion project templates

`Assimalign.Viu.Cohesion.Templates` contains the `viu-cohesion` template for a Viu Browser
WebAssembly application hosted during development by Cohesion Web.

Configure the Assimalign GitHub Packages source with a personal access token granting
`read:packages`, then install the template and create an application:

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
```

The generated `NuGet.config` uses the same GitHub feed and reads these environment variables;
nuget.org remains configured for public dependencies.

From the generated directory, run `dotnet watch`. The Viu Browser SDK delegates development
hosting to `Assimalign.Viu.Cohesion.DevServer`, whose package build target supplies the command
and readiness prefix expected by the SDK.
