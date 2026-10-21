# Assimalign.Cohesion.Viu.Server

`Assimalign.Cohesion.Viu.Server` is the host bridge between a Viu application and a Cohesion Web
pipeline. Cohesion's application builder is the guiding composition surface:

```csharp
WebApplicationBuilder builder = WebApplication.CreateBuilder();
builder.Services.AddViuClient(options =>
{
    options.ContentRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
});

WebApplication app = builder.Build();
app.UseViuApplication();
```

The public verbs are:

- `services.AddViuClient` registers browser assets; `AddViuApplication` remains an adapter on `IWebApplicationBuilder`.
- `services.AddViuServerRendering` composes host-owned SSR using generated catalogs by default.
- `UseViuApplication` serves a browser application's static-web-assets graph, its single-page
  application fallback, and the development browser-refresh HTTP surface.
- `UseViuServerRenderer` adapts Viu's host-neutral `ServerRenderAdaptor<IHttpContext>` to a
  Cohesion response stream.

The library consumes `Assimalign.Cohesion.Sdk.Web`, so Cohesion's `App` and `App.Web` shared
frameworks provide the HTTP, file-system, hosting, and feature assemblies. Its ordinary NuGet
dependencies are `Assimalign.Cohesion.Viu.Hosting` `10.0.0-preview.1`,
`Assimalign.Cohesion.FileSystem.Globbing` (not a framework member) at the Cohesion version the
package was built against, and `Assimalign.Viu.ServerRenderer` at the Viu pin in `global.json`. The original
pipeline-only `UseViuApplication(Action<ViuApplicationOptions>?)` remains available for custom
pipeline implementations; on a built Cohesion application, omitting the callback uses the
configuration previously registered by `AddViuApplication`.

The package's framework references are name-only NuGet metadata, so a direct package consumer must
also use Cohesion's SDK, at the version this package's Globbing dependency names, to supply the
targeting/runtime-pack mapping:

```xml
<Project Sdk="Assimalign.Cohesion.Sdk.Web">
  <ItemGroup>
    <PackageReference Include="Assimalign.Cohesion.Viu.Server" Version="10.0.0-preview.1" />
  </ItemGroup>
</Project>
```

Pin both SDKs because `Sdk.Web` chains the base SDK through an unversioned import:

```json
{
  "msbuild-sdks": {
    "Assimalign.Cohesion.Sdk": "<cohesion-version>",
    "Assimalign.Cohesion.Sdk.Web": "<cohesion-version>"
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

Compose server rendering in the Web host's single container:

```csharp
using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Cohesion.Viu.Server;
using Assimalign.Cohesion.Web.Hosting;
using Assimalign.Viu.Components;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddSingleton<ITaskCatalog>(catalog)
    .AddViuServerRendering(rendering =>
    {
        rendering.RootComponent = ComponentNode.For<ServerReport>();
        rendering.DocumentShell = _ => ReportDocumentShell.Instance;
    })
    .AddViuClient();

await using WebApplication application = builder.Build();
application.UseViuServerRenderer(context => context.Request.Path.Value == "/report");
application.UseViuApplication();
await application.RunAsync();
```

`rendering.UseRootComponent<ServerReport>()` is equivalent to assigning the typed node.
Generated component and render aggregates include application and referenced Viu assemblies.
Server's packaged compiler bridge publishes referenced catalogs even when Web uses only the
Cohesion SDK. Viu SDK projects compile application-local `.viu` components; the bridge does not
parse templates. Set `ViuAutoRegisterGeneratedCatalogs=false` to use explicit catalogs only.
`Components` and `ServerRenders` replace those defaults independently. The render registry freezes
at Web build; root, shell factory, state, directives, and diagnostics are captured then. Repeated
`AddViuServerRendering` calls configure one composition. Unset warning/error handlers write to
standard error. Component services come from the Web container through `Context.Services`.

The Web host starts, stops, and disposes the composition runtime. No separate Viu host or provider
is built. Every accepted render owns a fresh `ApplicationContext`, `ServerRenderApplication`,
`SsrContext`, and asynchronous Cohesion DI scope. Scoped services and unset state/directive
resolvers use that scope; singleton services remain shared. Viu tears down components before the
scope releases its resources, including after failure or cancellation. Explicit state/directive
instances are borrowed and shared. Cohesion's `IHttpContext` has no request-provider seam, so the
scope belongs to this render, is not shared with other Web middleware, and does not inject HTTP
context into services. The document-shell factory receives HTTP context directly.

The earlier `ConfigureServerRendering`, `AddViuServerApplication(ViuApplication)`, and
`UseViuServerRenderer(ViuApplication, ServerRenderRegistry, ...)` overloads remain available for
caller-owned composition. They preserve their borrowed provider and caller-owned disposal behavior.
The explicit `ServerRenderAdaptor<IHttpContext>` overloads remain the advanced seam for custom
request roots, service providers, and scopes. Custom pipeline implementations supply an adaptor;
the predicate-only overload requires a built Cohesion `WebApplication`.

All renderer overloads produce HTML with `Cache-Control: no-store`, propagate request cancellation,
and pass through non-GET requests, reserved resource paths, and requests rejected by the predicate.
The document shell preserves `prefix -> progressive render -> teleport suffix` order. Destinations
belong in the suffix; already emitted head/body content needs buffering or a prepass to change.
Cohesion currently buffers responses, so a render/adaptor failure before transport commitment can
clear the body, set 500, and propagate to the host exception boundary. A component error handled by
the configured Viu error handler follows Viu's normal handled-error semantics.

## Web resource and client bundling

Version `10.0.0-preview.1` supports Cohesion 10.0 preview Web resources. On the executable,
declare `<ProjectReference Include="../Client/Client.csproj" ViuClient="true" />` and call
`builder.Services.AddViuClient()` before building the host. A normal Web build restores the client
through the project graph; no separate client restore is needed. The package automatically applies
`ReferenceOutputAssembly=false` and `Private=false` while preserving build order.
Package `buildTransitive` assets establish build order, query `GetViuBrowserClientInfo` for the client identity, output and exact manifest paths, and write them to
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

`UseClientOutput()` infers the sole client from that document; the named overload still validates
an explicit identity. In an `AddViuClient` callback, call `options.UseClientOutput()` before
customizing fallback or headers, or supply explicit content/manifest paths. The old
`ViuClientReference` item and `ViuClientProject` property now fail with a migration error.
