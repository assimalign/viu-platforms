# Assimalign.Cohesion.Viu.ApplicationModel

Version `10.0.0-preview.1`. This apphost-side package validates the Viu facts on a Cohesion Web
manifest and exposes a typed site descriptor. It layers on the Cohesion build the repository's
`global.json` pins. It has no Viu runtime or Web hosting dependency.

## Usage

```csharp
using Assimalign.Cohesion.ApplicationModel;

IApplicationBuilder builder = Application.CreateBuilder("docs", args);
ResourceManifest manifest = ResourceManifest.Load("resource.json");
IViuWebResourceDescriptor site = builder.AddViuWeb(manifest,
    new ViuWebResourceOptions { Replicas = 2 });
```

`AddViuWeb` requires `Kind=Web` and valid `web.viu.client`, `web.viu.spa-fallback`, and
`web.viu.base-path` properties. Declare a `ProjectReference` marked `ViuClient="true"` on an enabled
`Assimalign.Cohesion.Sdk.Web` executable referencing Server to produce these facts. Like every
Cohesion ApplicationModel package, this one declares the shared `Assimalign.Cohesion.ApplicationModel`
namespace, so an apphost composes it with one `using`. `ValidateClient=false` skips Viu-property
validation for migration; the Web kind remains required. `RemoteReferenceViuWeb` offers the same
typed wrapper for a manifest-backed remote Web binding.

The descriptor exposes `Web`, `ClientAssembly`, `SpaFallback`, `BasePath`, dependencies and commands.
Planning remains entirely with `AddWeb`; this package contributes no gateway or control plane.

## Generated gateway verb

The generated `Add<Name>()` verb for a Web manifest emits `AddWeb`. Cohesion must add an appropriate
`CohesionGatewayResourceKind` mapping before generated composition can choose `AddViuWeb`.
Web executables retain `Assimalign.Cohesion.Web.ApplicationModel`; do not relabel their resource kind
or application-model identity to force selection. Until that SDK follow-up, an apphost calls
`AddViuWeb(Manifests.<Name>)` explicitly, as the `viu-cohesion-web` template does. See
[design](DESIGN.md).

The Browser SDK's `GetViuBrowserClientInfo` supplies the client assembly identity written into
`web.viu.client`. Restore/build graph participation comes from the normal project reference;
no Viu runtime or reflection discovery is introduced into this apphost package.
