# Design

## Hosting boundary

The server uses `Assimalign.Cohesion.Sdk.Web` at the `global.json` pin and the App/App.Web shared frameworks.
`WebApplication.CreateBuilder()` is the composition root: `AddViuApplication` registers the bridge,
`builder.Server.UseServer(...UseHttp1...)` configures every resolved URL, and the built application
installs the registered middleware through `UseViuApplication`.

The current Cohesion builder registers its default server as `IWebApplicationServer`, not as an
`IHostService`. Consequently `WebApplication.RunAsync()` does not start that server. The DevServer
follows Cohesion's own integration-test pattern: it resolves the builder-created
`IWebApplicationServer`, calls `StartAsync`, probes every bound endpoint before announcing
readiness, waits for the shutdown token, and calls `StopAsync` to drain connections. This is a
current upstream hosting-lifecycle gap, not a return to the removed custom listener loop.

## URL policy

Only clear-text HTTP is accepted because the package has no certificate/TLS CLI contract. `localhost`, IP literals, and resolvable IPv4 host names are supported. URL paths, queries, and fragments are rejected. The default is an explicitly reserved `127.0.0.1` port. Reserving and releasing a port before Cohesion binds it has the usual small inter-process race; Cohesion's transport does not expose a bound-endpoint result for a port-zero listener.

## Package target

The target is evaluation-time configuration because IDE/F5 consumers read the Viu SDK's run properties without necessarily executing a custom target. Paths are quoted and normalized to absolute paths. `WasmEnableThreads=true` appends `--apply-cop-headers`; URL selection is deliberately left to `ASPNETCORE_URLS` or the server's loopback default.

The NuGet pack invokes a framework-dependent nested publish and places the complete publish
directory under `tools/`. A private publish property marks the App/App.Web runtime packs
`RuntimePackAlwaysCopyLocal`; the build host RID selects those packs, whose closure is managed
assemblies. The runtimeconfig therefore retains only `Microsoft.NETCore.App`, while Cohesion and
Viu assemblies are carried beside the tool. No Cohesion shared-framework installation, app host,
or self-contained Microsoft runtime is required on the consumer machine.

## Portable tool payload

`tools/` runs under the consumer's `dotnet exec`, on whatever architecture that host is: win-x64,
win-arm64, linux-x64, linux-arm64, or osx-arm64. CI packs on Linux x64, so the payload must not
depend on the packing machine's architecture. It is one portable layout of AnyCPU IL assemblies.

The publish still needs a RuntimeIdentifier. App and App.Web ship only as RID-named runtime packs
(`Assimalign.Cohesion.App.Runtime.<rid>`), and the .NET SDK resolves runtime-pack assets only for a
RID. A RID-less publish drops the App/App.Web closure without an error: only the Cohesion
assemblies that arrive as ordinary NuGet packages remain, and the tool cannot start.

The closure itself does not vary by RID. Across the win-arm64 and linux-x64 packs every assembly is
byte-identical AnyCPU IL except each framework's anchor assembly (`Assimalign.Cohesion.App.dll`,
`Assimalign.Cohesion.App.Web.dll`). An anchor defines no types and no assembly references it, but
it is compiled for its pack's RID (PE machine AMD64 or Arm64). The publish removes anchors from
`RuntimePackAsset`, which keeps them out of both the directory and the deps.json. The tool
assembly sets `PlatformTarget=AnyCPU`, because the SDK otherwise derives the platform target from
the RuntimeIdentifier. The setting applies to every build, not only the nested publish: the
compiler's incremental cache does not track `PlatformTarget`, so a nested publish that differed
from the outer build could reuse a stale image. The deps.json still names the packing RID
(`.NETCoreApp,Version=v10.0/linux-x64`). The framework-dependent host loads app-local assets from
it on any RID; a linux-x64 pack starts under both win-arm64 and win-x64 `dotnet`.

Per-RID payload folders (`tools/<rid>/`, selected in the build target by
`NETCoreSdkRuntimeIdentifier`) were rejected. They would multiply the 4.5 MB payload by five, make
every pack download five runtime-pack pairs, and add a selection step that could pick the wrong
host. None of that buys anything while the closure is RID-neutral.

`Assert-ViuPlatformsPortableAssembly` (`scripts/modules/ViuPlatformsPackaging.psm1`) fails
`Pack-Release.ps1` when any package carries a `.dll` or `.exe` outside `runtimes/` that is not PE
machine I386 with ILONLY, or that is 32BITREQUIRED or ReadyToRun. If a future Cohesion runtime
pack ships a RID-specific implementation or a native asset, the pack fails there. That is the
point to revisit per-RID folders. `ToolAssemblyPortabilityTests` pins the tool assembly itself.

## Constraints and omitted surface

- This host is development-only and supports HTTP/1.1. TLS, HTTP/2, HTTP/3, production hardening, and access logging are outside its contract.
- Tests cover parsing, property selection, readiness text, builder-level composition, and a real loopback HTTP transport.
- The optional browser debug websocket proxy is not implemented in this phase. Browser refresh and hot reload are provided by `Assimalign.Cohesion.Viu.Server`.
- AOT compatibility is not asserted for this executable. The shipping middleware library owns the AOT-sensitive JSON and rendering paths; this app is shipped as a normal framework-dependent tool payload.

## Preview.3 rebase

The tool and its tests resolve the Web SDK through the `global.json` pin, avoiding mixed SDK resolver
versions. The Server dependency remains a library despite the new Web SDK's executable default.
Packing still publishes the managed Cohesion framework closure into `tools`; the deployable Web
resource template consumes Server's client bundling targets directly instead of shipping this tool.
