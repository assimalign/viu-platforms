# Design

## Hosting boundary

The server uses `Assimalign.Cohesion.Sdk.Web/10.0.1-preview.3.local` and the App/App.Web shared frameworks.
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

## Constraints and omitted surface

- This host is development-only and supports HTTP/1.1. TLS, HTTP/2, HTTP/3, production hardening, and access logging are outside its contract.
- Tests cover parsing, property selection, readiness text, builder-level composition, and a real loopback HTTP transport.
- The optional browser debug websocket proxy is not implemented in this phase. Browser refresh and hot reload are provided by `Assimalign.Cohesion.Viu.Server`.
- AOT compatibility is not asserted for this executable. The shipping middleware library owns the AOT-sensitive JSON and rendering paths; this app is shipped as a normal framework-dependent tool payload.

## Preview.3 rebase

The tool and its tests resolve the same preview.3.local Web SDK, avoiding mixed SDK resolver
versions. The Server dependency remains a library despite the new Web SDK's executable default.
Packing still publishes the managed Cohesion framework closure into `tools`; the deployable Web
resource template consumes Server's client bundling targets directly instead of shipping this tool.
