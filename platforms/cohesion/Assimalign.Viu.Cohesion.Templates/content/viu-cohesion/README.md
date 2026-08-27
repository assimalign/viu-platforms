# ViuCohesionApplication

This project is a Viu Browser WebAssembly application hosted during development by Cohesion Web.

## Local feeds

`NuGet.config` points to the three package feeds selected when the project was created:

- Viu packages, including `Assimalign.Viu.Sdk.Browser`;
- Cohesion packages used by the development server; and
- Viu Platforms packages, including `Assimalign.Viu.Cohesion.DevServer`.

The default values target the Phase 2 integration checkouts. Update the paths if the repositories
or feeds move.

## Development loop

Restore once, then start the normal Viu watch loop:

```powershell
dotnet restore
dotnet watch
```

The dev-server package changes the Viu Browser SDK's host to Cohesion. The SDK builds the
WebAssembly application, launches the packaged server, waits for `Now listening on:`, and opens
the reported application URL. Browser refresh is active when `dotnet watch` supplies
`ASPNETCORE_AUTO_RELOAD_WS_ENDPOINT`.

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
