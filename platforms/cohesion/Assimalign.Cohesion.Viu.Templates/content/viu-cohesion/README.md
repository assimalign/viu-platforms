# CohesionViuApplication

This project is a Viu Browser WebAssembly application composed through Cohesion hosting and served
during development by Cohesion Web. Its Cohesion Viu hosting and development-server packages use
platform version `10.0.0-preview.1`.

## Package feed

The generated `NuGet.config` maps the Assimalign sibling local feeds and nuget.org. Pass
`--assimalignRoot C:/Source/repos/assimalign` when creating the template outside that directory.
This family tracks Cohesion 10.0.0-preview.1.local and Viu beta.12 until their release feeds are available.

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

The project opts into preview features because Cohesion `10.0.0-preview.1.local` annotates its public
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
