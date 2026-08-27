# Assimalign.Viu.Cohesion.DevServer

`Assimalign.Viu.Cohesion.DevServer` is the package-integrated development host for Viu browser applications served by Cohesion Web. A direct `PackageReference` supplies a `build/` target that selects the Viu SDK's custom-server path and a framework-dependent server payload under `tools/`.

The executable accepts the fixed integration contract:

```text
dotnet exec Assimalign.Viu.Cohesion.DevServer.dll
  --content-root <absolute wwwroot path>
  --endpoints-manifest <absolute staticwebassets.endpoints.json path>
  --runtime-manifest <absolute staticwebassets.runtime.json path>
  [--apply-cop-headers]
  [--urls <semicolon-separated HTTP URLs>]
```

`ASPNETCORE_URLS` has precedence over `--urls`. With neither setting, the server reserves an available IPv4 loopback port and binds an explicit URL. Every successful bind writes both `Now listening on: <url>` and `App url: <url>`; the first line is the Viu RunHost and dotnet-watch readiness contract.

The application composes `Assimalign.Viu.Cohesion.Web` for manifest-driven assets, SPA fallback, content types, compression selection, optional cross-origin isolation headers, and the browser-refresh surface. The server itself owns Cohesion's HTTP transport listener lifetime.

## Package behavior

Unless `ViuBrowserDevServerCommand` is already set, the package target sets:

- `ViuBrowserDevServer=Custom`
- `ViuBrowserDevServerCommand=dotnet`
- `ViuBrowserDevServerArguments` to `dotnet exec` the packaged DLL with the Viu build's output and manifest paths
- `ViuBrowserDevServerReadinessPrefix=Now listening on:`

Package presence therefore wins over the Viu SDK's default `WasmAppHost` choice. To opt out while retaining the package reference, explicitly set `ViuBrowserDevServerCommand` and the related custom-server properties; setting only `ViuBrowserDevServer=WasmAppHost` is intentionally not an override in this initial integration.

The payload is published without an app host, runtime identifier, or self-contained runtime. Cohesion and Viu runtime assemblies are copy-local under `tools/`; the consumer needs only a compatible Microsoft .NET 10 runtime.
