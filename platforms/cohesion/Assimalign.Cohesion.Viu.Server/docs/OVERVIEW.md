# Assimalign.Cohesion.Viu.Server

`Assimalign.Cohesion.Viu.Server` is the host bridge between a Viu application and a Cohesion Web
pipeline. Cohesion's application builder is the guiding composition surface:

```csharp
WebApplicationBuilder builder = WebApplication.CreateBuilder();
builder.AddViuApplication(options =>
{
    options.ContentRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
});

WebApplication app = builder.Build();
app.UseViuApplication();
```

The public verbs are:

- `AddViuApplication` captures immutable application configuration on `IWebApplicationBuilder`.
- `UseViuApplication` serves a browser application's static-web-assets graph, its single-page
  application fallback, and the development browser-refresh HTTP surface.
- `UseViuServerRenderer` adapts Viu's host-neutral `ServerRenderAdaptor<IHttpContext>` to a
  Cohesion response stream.

The library consumes `Assimalign.Cohesion.Sdk.Web/10.0.1-preview.3.local`, so Cohesion's `App` and
`App.Web` shared frameworks provide the HTTP, file-system, hosting, and feature assemblies. Its
ordinary NuGet dependencies are `Assimalign.Cohesion.Viu.Hosting` `10.0.0-beta.5` and
`Assimalign.Viu.ServerRenderer` `10.0.0-beta.12`. The original
pipeline-only `UseViuApplication(Action<ViuApplicationOptions>?)` remains available for custom
pipeline implementations; on a built Cohesion application, omitting the callback uses the
configuration previously registered by `AddViuApplication`.

The package's framework references are name-only NuGet metadata, so a direct package consumer must
also use Cohesion's SDK to supply the preview.3.local targeting/runtime-pack mapping:

```xml
<Project Sdk="Assimalign.Cohesion.Sdk.Web/10.0.1-preview.3.local">
  <ItemGroup>
    <PackageReference Include="Assimalign.Cohesion.Viu.Server" Version="10.0.0-beta.5" />
  </ItemGroup>
</Project>
```

Pin both SDKs because `Sdk.Web` chains the base SDK through an unversioned import:

```json
{
  "msbuild-sdks": {
    "Assimalign.Cohesion.Sdk": "10.0.1-preview.3.local",
    "Assimalign.Cohesion.Sdk.Web": "10.0.1-preview.3.local"
  }
}
```

## Browser application guarantees

The asset catalog understands both .NET 10 static-web-assets manifests:

- `<App>.staticwebassets.runtime.json` supplies ordered content roots and its trie of exact assets
  and `**` patterns.
- `<App>.staticwebassets.endpoints.json` supplies public routes, fingerprinted asset names,
  response headers, and content-encoding selectors.

Manifest JSON is deserialized through generated `System.Text.Json` metadata. The library does not
use reflection serialization, scan assemblies, or parse manifests per request. Resolved physical
paths must remain beneath their declared content root. Endpoint routes stay ordinal; physical
runtime routes follow the operating system's file-name comparison.

When an endpoint has Brotli, gzip, and identity variants, the request's `Accept-Encoding` quality
and the manifest selector quality choose the result. The manifest's response headers are preserved.
Viu and WebAssembly extensions have explicit content types, including `.wasm`, `.webcil`, `.dll`,
`.pdb`, `.dat`, `.blat`, `.mjs`, `.cjs`, `.json`, and `.md` (`text/markdown; charset=utf-8`, so
documentation content fetched by the Markdown package is typed correctly). Unknown non-file GET and HEAD navigation
routes fall back to identity `index.html` with `Cache-Control: no-store`; missing file-like and
`/_framework` routes continue so the host can answer 404. Disable `EnableSpaFallback` when a
downstream server renderer owns application documents.

This first-pass host preserves manifest cache headers but does not yet evaluate conditional or
range requests. `If-Match`, `If-None-Match`, `If-Modified-Since`, `Range`, and `If-Range` therefore
receive a complete 200 response. Cohesion StaticFiles is available through App.Web, but this bridge
retains its manifest-aware resolver; delegating conditional/range policy without losing endpoint
variants and static-web-assets routing remains future work.

`ApplyCrossOriginIsolationHeaders` applies these headers to every response observed by the Viu
middleware:

```text
Cross-Origin-Embedder-Policy: require-corp
Cross-Origin-Opener-Policy: same-origin
```

## Development refresh

Refresh activates only when `ASPNETCORE_AUTO_RELOAD_WS_ENDPOINT` is non-empty. The middleware then
injects the standard browser-refresh script element immediately before `</body>` in identity HTML
and exposes:

- `/_framework/aspnetcore-browser-refresh.js`
- `/_framework/blazor-hotreload`
- `/_framework/clear-browser-cache`

The vendored client honors comma-separated WebSocket endpoints and the dotnet-watch RSA-OAEP,
SHA-256 key-subprotocol handshake. It handles `Reload`, `Wait`, `UpdateStaticFile`, and
`ApplyManagedCodeUpdates`. The delta cache uses the dotnet-watch GET/POST shape: empty GET is 204,
POST accepts `{ "id": number, "deltas": [] }`, and only increasing identifiers are retained.

This is a deliberately minimal compatible client, not a byte-for-byte copy of Microsoft's private
resource. `ReportDiagnostics`, `GetApplyUpdateCapabilities`, and `RefreshBrowser` are not currently
implemented. Managed deltas are applied through Blazor's browser hook when that hook is present and
are always cached for boot-time retrieval; Viu does not yet publish an independent managed-delta
application hook. Browser-refresh boot-resource header rewriting is also outside this first pass.
Manifest catalogs are process snapshots; a changed route graph requires the development server to
restart, while updates to already resolved physical files are visible immediately.

## Server rendering

The hosting-model front door composes one server-side Viu application with the same builder used by
the Browser host. `ConfigureServerRendering` selects a composition-only runtime: it owns a genuine
single-use Viu lifetime and application-middleware pipeline, but it never mounts a tree and never
retains a `ServerRenderApplication`:

```csharp
ComponentFactory components = new();
GeneratedViuComponents.Register(components);

ViuApplicationBuilder viuBuilder = ViuApplication.CreateBuilder()
    .ConfigureServerRendering()
    .ConfigureApplication(options =>
    {
        options.RootComponent = new ComponentNode(
            ComponentReference.ForName("app-root"));
        options.Components = components;
    });

ViuApplication viuApplication = viuBuilder.Build();

ServerRenderRegistry serverRenders = new();
GeneratedViuServerRenders.Register(serverRenders);

WebApplicationBuilder webBuilder = WebApplication.CreateBuilder();
webBuilder.AddViuServerApplication(viuApplication);
WebApplication webApplication = webBuilder.Build();
webApplication.UseViuServerRenderer(viuApplication, serverRenders);
```

`AddViuServerApplication` must run before the Cohesion Web application is built. It installs an
`IHostService` adapter that starts and stops the borrowed Viu application with the Web host. It does
not dispose that application; the caller owns it and must dispose it after the Web host. Viu
application middleware surrounds this persistent composition lifetime, not individual HTTP
requests.

The hosting-model `UseViuServerRenderer` overload freezes the supplied `ServerRenderRegistry` at
pipeline composition and borrows a private snapshot of the Viu application's root, components,
services, state definitions, directives, and diagnostics. Every accepted request receives a fresh
`ApplicationContext`, `ServerRenderApplication`, and `SsrContext`. Nothing reflection-activates a
component, and the shared generated registry is immutable before concurrent lookup.

This first hosting-model bridge deliberately borrows the application's root service provider. It
does not create a Cohesion child DI scope, inject `IHttpContext` into services, or select a
request-specific root. Applications requiring those capabilities continue to use the explicit
`ServerRenderAdaptor<IHttpContext>` overload and provide their own
`IServerRenderRequestScopeFactory<IHttpContext>`. That is also the escape hatch for owned
request-scoped resources. The Viu runtime does not currently provide a stronger generic mapping
between Cohesion HTTP scopes and component composition.

The server-render verb receives an already configured `ServerRenderAdaptor<IHttpContext>` and a
request-root factory. The basic overload calls `RenderAsync`; the document overload additionally
receives a `Func<IHttpContext, IServerRenderDocumentShell>`, creates a shell for that request, and
calls `RenderDocumentAsync`. Both set an HTML/no-store response, create a typed
`ServerRenderRequest`, transcode Viu's character chunks to UTF-8, await response writes and flushes,
and propagate `IHttpContext.RequestCancelled`. They never own or dispose the Cohesion context or
response stream. Non-GET requests and GET requests rejected by the optional predicate continue
through the pipeline.

The current response body is a replaceable memory buffer, so `CohesionServerRenderOutput` reports
`ResponseCommitted == false`; attempted writes and flushes do not imply transport commitment. On a
render failure, encoder completion observes `RequestCancelled` and finishes before the middleware
clears the buffered body and installs a clean 500 response. The failure is then rethrown for the
application's exception boundary. A future streaming implementation must delegate
`ResponseCommitted` to Cohesion's monotonic, transport-confirmed `HasStarted` state. If that state is
true, a later render or teardown failure leaves the committed response intact before being rethrown;
Viu snapshots the host's value after request-scope teardown.

The consuming application owns request-scoped Viu composition. Its
`IServerRenderRequestScopeFactory<IHttpContext>` must return a fresh `ServerRenderApplication` and
`SsrContext` using the exact requested root instance. Generated component and server-render
registrations remain explicit in the consuming assembly; this bridge performs no discovery. Populate
the registry during application composition and freeze it before publishing the adaptor to concurrent
requests:

```csharp
ServerRenderRegistry registry = new();
GeneratedViuServerRenders.Register(registry);

IServerRenderRegistry serverRenders = registry.Freeze();
ServerRenderAdaptor<IHttpContext> adaptor =
    new(requestScopeFactory, serverRenders);
```

`Freeze` is idempotent, publishes an immutable lock-free resolution snapshot, and rejects later
registration. The adaptor passes the original request-abort token to
`IServerRenderRequestScope.DisposeAsync(CancellationToken)` on success, failure, cancellation, and
partial-output teardown; an abort-aware scope can observe it while still releasing its resources.

The document overload preserves exact `prefix -> progressive main -> teleport suffix` ordering. A
non-empty host prefix is flushed before component execution. After the main render succeeds and all
teleports resolve, the suffix receives their stable target-to-payload map. Every destination belongs
in that suffix, and the shell writes each payload verbatim because it already contains Viu's
hydration anchor. Prefix, main-render, output, or cancellation failure suppresses the suffix.

Teleport insertion is deliberately suffix-only. The shell cannot backpatch an already streamed
prefix or `<head>`, or splice into a previously emitted main-body region. Earlier destinations require
the host to buffer that region or perform a render prepass.

## Web resource and client bundling

Version `10.0.0-beta.5` supports Cohesion `10.0.1-preview.3.local` Web resources. On the executable,
set `ViuClientProject` to the browser csproj and call
`builder.AddViuApplication(o => o.UseClientOutput("Acme.Docs.Client"))` before building the host.
Package `buildTransitive` assets establish build order, write the evaluated client output path to
a `viu-client.json` content file that travels beside the executable and into an in-process
member's resource content root, publish the client in Release with its own settings, and bundle
its complete `wwwroot` into Web's publish directory. Published hosting selects the physical root without manifests, including
fingerprinted assets, MIME types, and negotiated `.br`/`.gz` siblings.

Enabled Web manifests gain `web.viu.client`, `web.viu.spa-fallback=true`, and `web.viu.base-path=/`.
Viu middleware passes `/healthz`, `/readyz`, `/livez`, and the resource control-plane path
(`ViuApplicationOptions.ControlPlanePath`, default `/cohesion/v1`, plus its subtree) through before fallback or
server rendering. The default `/_cohesion/client.json` route serves only public runtime identity
and reference URLs as `cohesion/viu-client/v1`, with `no-store`. Set `ClientConfigurationPath` to a
local absolute route to move it, or null to disable it. Plain hosts use their host environment and
empty references. See [design](DESIGN.md) for the build boundary and gateway limitation.
