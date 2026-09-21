# Assimalign.Cohesion.Viu.ApplicationModel

Version `10.0.0-preview.1`. This apphost-side package validates the Viu facts on a Cohesion Web
manifest and exposes a typed site descriptor. It layers on Cohesion `10.0.0-preview.1`, consumed
as `10.0.0-preview.1.local` in the sibling-feed loop. It has no Viu runtime or Web hosting dependency.

## Usage

```csharp
using Assimalign.Cohesion.ApplicationModel;
using Assimalign.Cohesion.Viu.ApplicationModel;

IApplicationBuilder builder = Application.CreateBuilder("docs", args);
ResourceManifest manifest = ResourceManifest.Load("resource.json");
IViuWebResourceDescriptor site = builder.AddViuWeb(manifest,
    new ViuWebResourceOptions { Replicas = 2 });
```

`AddViuWeb` requires `Kind=Web` and valid `web.viu.client`, `web.viu.spa-fallback`, and
`web.viu.base-path` properties. Set `ViuClientProject` on an enabled `Assimalign.Cohesion.Sdk.Web`
executable referencing Server to produce these facts. `ValidateClient=false` skips Viu-property
validation for migration; the Web kind remains required. `RemoteReferenceViuWeb` offers the same
typed wrapper for a manifest-backed remote Web binding.

The descriptor exposes `Web`, `ClientAssembly`, `SpaFallback`, `BasePath`, dependencies and commands.
Planning remains entirely with `AddWeb`; this package contributes no gateway or control plane.

## Generated gateway limitation

`AddAllResources()` still emits `AddWeb`. Cohesion must add an appropriate
`CohesionGatewayResourceKind` mapping before generated composition can choose `AddViuWeb`.
Web executables retain `Assimalign.Cohesion.Web.ApplicationModel`; do not relabel their resource kind
or application-model identity to force selection. Until that SDK follow-up, call `AddViuWeb`
explicitly when apphost-side validation is needed. See [design](DESIGN.md).
