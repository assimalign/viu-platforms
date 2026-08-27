# Design

## Hosting boundary

The local Cohesion feed's `Assimalign.Cohesion.Sdk.Web` 10.0.1-preview.1 targeting/runtime packs do not contain the hosting family required to build this application. The server therefore uses plain `Microsoft.NET.Sdk`, `Assimalign.Cohesion.Web.Server` 10.0.2, and the project/package reference to `Assimalign.Viu.Cohesion.Web`. This is the packaged-consumer path that restores and builds on the current feed.

The 10.0.2 package surface exposes the concrete transport API through `Assimalign.Cohesion.Http.Transports` and `Assimalign.Cohesion.Transports`. The dev server creates one `HttpConnectionListener` per configured URL and owns the following lifecycle:

1. Start every accept loop as a tracked `Task`.
2. Probe each bound TCP endpoint before announcing readiness.
3. Track every accepted connection task until it completes.
4. On Ctrl+C or SIGTERM, cancel the shared shutdown token and dispose all listeners to unblock accept.
5. Await accept loops, then await the stable in-flight connection snapshot.

Each connection is disposed deterministically. Every received `IHttpContext` runs through the composed Cohesion pipeline, is sent once, and is then disposed. An application or wire failure aborts only that connection. The implementation does not use the feed's `WebApplicationServer`; this keeps shutdown ownership and task observation explicit in the executable.

## URL policy

Only clear-text HTTP is accepted because the package has no certificate/TLS CLI contract. `localhost`, IP literals, and resolvable IPv4 host names are supported. URL paths, queries, and fragments are rejected. The default is an explicitly reserved `127.0.0.1` port. Reserving and releasing a port before Cohesion binds it has the usual small inter-process race; Cohesion's transport does not expose a bound-endpoint result for a port-zero listener.

## Package target

The target is evaluation-time configuration because IDE/F5 consumers read the Viu SDK's run properties without necessarily executing a custom target. Paths are quoted and normalized to absolute paths. `WasmEnableThreads=true` appends `--apply-cop-headers`; URL selection is deliberately left to `ASPNETCORE_URLS` or the server's loopback default.

The NuGet pack invokes a framework-dependent, no-RID `dotnet publish` and places the complete publish directory under `tools/`. This makes Cohesion framework assemblies copy-local and avoids requiring the unpublished `Assimalign.Cohesion.App.Web` shared framework on a consumer machine.

## Constraints and omitted surface

- This host is development-only and supports HTTP/1.1. TLS, HTTP/2, HTTP/3, production hardening, and access logging are outside its contract.
- Cohesion has no packaged Web testing host in the feed, so tests cover parsing, property selection, readiness text, and a real loopback HTTP transport.
- The optional browser debug websocket proxy is not implemented in this phase. Browser refresh and hot reload are provided by `Assimalign.Viu.Cohesion.Web`.
- AOT compatibility is not asserted for this executable. The shipping middleware library owns the AOT-sensitive JSON and rendering paths; this app is shipped as a normal framework-dependent tool payload.
