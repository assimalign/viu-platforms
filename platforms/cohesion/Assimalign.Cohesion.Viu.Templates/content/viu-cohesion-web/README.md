# CohesionViuSite

The Client runs Viu in the browser. Web is an enabled Cohesion Web resource serving that client,
its public `/_cohesion/client.json` document, and Cohesion health/control-plane routes. AppHost
composes Web through the built-in Local gateway. All project SDKs are pinned in `global.json`.

## Local packages

This template pins Viu `10.0.0-preview.0.pr.379.5`, Cohesion `10.0.0-preview.0.ci.21`,
and platform packages `10.0.0-preview.1`. Pack the local platform family before building.
When creating the template outside the sibling checkout directory, pass
`--assimalignRoot C:/Source/repos/assimalign`. The `--cohesionVersion` parameter selects another
complete SDK/framework/library identity. Keep SDK and library identities aligned.

```pwsh
dotnet build
dotnet run --project CohesionViuSite.Web
dotnet run --project CohesionViuSite.AppHost -- --gateway local --mode describe
```

Web defaults to HTTP port 8080. `COHESION_ENDPOINT_HTTP_PORT` selects another port.
The client configuration provider is populated before components start; failed document requests
warn and retain an empty configuration. The supplied `HttpClient` belongs to Program.

## Publish and containers

A normal `<ProjectReference Include="../Client/Client.csproj" ViuClient="true" />` joins the restore
graph and establishes build order without a compile reference. The Browser SDK identity query
supplies exact manifest paths; `builder.Services.AddViuClient()` infers the single client.
Building Web restores Client automatically; no separate restore is required. Publishing Web
publishes the client in Release using its own trimming settings, then bundles its entire physical
`wwwroot`, including framework assets and compressed siblings. Published hosting needs no manifests.

```pwsh
dotnet publish CohesionViuSite.Web -c Release -r linux-arm64
dotnet msbuild CohesionViuSite.Web/CohesionViuSite.Web.csproj -t:CohesionPublishImage -p:Configuration=Release -p:RuntimeIdentifier=linux-x64 -p:CohesionContainerRepository=docs/site -p:CohesionContainerPush=false
```

The image target requires the Cohesion App and App.Web runtime packs for the chosen RID.
It produces an OCI archive without a Dockerfile or registry push. The current Cohesion image
resolver accepts only `linux-x64` and `linux-musl-x64`; `linux-arm64` ordinary publish works,
but its image target requires a Cohesion SDK follow-up. Release images require NativeAOT and a
supported Linux build host/toolchain. Debug permits a self-contained JIT image.
Kubernetes is opt-in: set `CohesionGateways` to `Local;Kubernetes` and
`CohesionPlatformsVersion` to gateway packages built against the same Cohesion identity.
`--gateway kubernetes --mode render` also needs image metadata and gateway configuration; it does not deploy.

## Application model

Web remains `Kind=Web` and declares `Assimalign.Cohesion.Web.ApplicationModel`. Its manifest carries
`web.viu.client`, `web.viu.spa-fallback`, and `web.viu.base-path`. AppHost composes it with
`AddViuWeb(Manifests.<Name>)`, which validates those facts and produces the same Web plan as the
generated `Add<Name>()` verb. Automatic Viu verb selection requires a Cohesion SDK
`CohesionGatewayResourceKind` mapping follow-up. Do not change Web's resource kind or application model.

Components use `builder.Services.AddComponents()`, `builder.UseRootComponent<App>()` and
`routing.Routes.Add<Counter>("/", "Counter")`. No catalog aliases or component-name strings are
needed. Supply GitHub Packages credentials with `NuGetPackageSourceCredentials_assimalign`;
no credentials are stored in the template.
