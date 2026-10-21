# CohesionViuApplication

This project is a Viu Browser WebAssembly application composed through Cohesion hosting and served
during development by Cohesion Web. Its Cohesion Viu hosting and development-server packages use
platform version `10.0.0-preview.1`.

## Package feed

The generated `NuGet.config` maps local platform feeds, GitHub Packages and nuget.org.
`--assimalignRoot C:/Source/repos/assimalign` when creating the template outside that directory.
The Viu SDK pin is `10.0.0-preview.0.pr.379.5`; Cohesion packages restore from GitHub Packages.

## Development loop

After restoring, start the normal Viu watch loop:

```powershell
dotnet watch
```

The application uses `ViuApplication.CreateBuilder()` and `ConfigureBrowser()` to compose its Viu
runtime, services, and configuration. The separate development-server package changes the Viu
Browser SDK's host to Cohesion: the SDK builds the WebAssembly application, launches the packaged
server, waits for `Now listening on:`, and opens the reported application URL. Browser refresh is
active when `dotnet watch` supplies `ASPNETCORE_AUTO_RELOAD_WS_ENDPOINT`.

The project opts into preview features because Cohesion 10.0 previews annotates its public
hosting surface as preview.

Publish a trimmed release build with:

```powershell
dotnet publish -c Release
```

The project permits a missing SDK `PrunePackageData` directory because some .NET 10 SDK
installations omit that optional metadata. Trimming itself remains enabled.

Ahead-of-time compilation is optional and requires the WebAssembly tools workload:

```powershell
dotnet publish -c Release -p:RunAOTCompilation=true
```

Components use `builder.Services.AddComponents()`, `builder.UseRootComponent<App>()` and
`routing.Routes.Add<Counter>("/", "Counter")`. No catalog aliases or component-name strings are
needed. Supply GitHub Packages credentials with `NuGetPackageSourceCredentials_assimalign`;
no credentials are stored in the template.
