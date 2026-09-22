# CohesionViuSite

The Client runs Viu in the browser. Web is an enabled Cohesion Web resource serving that client,
its public `/_cohesion/client.json` document, and Cohesion health/control-plane routes. AppHost
composes Web through the Local or Kubernetes gateway. All project SDKs are pinned in `global.json`.

## Local packages

This template tracks Cohesion `10.0.0-preview.1.local`, Viu `10.0.0-preview.1`, and the Viu hosting
family `10.0.0-preview.1`. Populate the four local feeds named in `NuGet.config` before building.
When creating the template outside the sibling checkout directory, pass
`--assimalignRoot C:/Source/repos/assimalign`. The `--cohesionVersion` parameter selects another
complete SDK/framework/library identity. Keep SDK and library identities aligned.

```pwsh
dotnet build
dotnet run --project CohesionViuSite.Web
dotnet run --project CohesionViuSite.AppHost -- --gateway local --mode describe
dotnet run --project CohesionViuSite.AppHost -- --gateway kubernetes --mode render
```

Web defaults to HTTP port 8080. `COHESION_ENDPOINT_HTTP_PORT` selects another port.
The client configuration provider is populated before components start; failed document requests
warn and retain an empty configuration. The supplied `HttpClient` belongs to Program.

## Publish and containers

A `ViuClientReference` item establishes build order and supplies build asset manifests. Publishing Web
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
Kubernetes render may require published image metadata and gateway configuration; it does not deploy.

## Application model

Web remains `Kind=Web` and declares `Assimalign.Cohesion.Web.ApplicationModel`. Its manifest carries
`web.viu.client`, `web.viu.spa-fallback`, and `web.viu.base-path`. AppHost composes it with
`AddViuWeb(Manifests.<Name>)`, which validates those facts and produces the same Web plan as the
generated `Add<Name>()` verb. Automatic Viu verb selection requires a Cohesion SDK
`CohesionGatewayResourceKind` mapping follow-up. Do not change Web's resource kind or application model.
