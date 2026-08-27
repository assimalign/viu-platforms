# Assimalign.Viu.Cohesion.Web design

## Hosting boundary

The bridge is middleware, not a Cohesion application. Public composition follows Cohesion's
builder-verb convention and stores immutable configuration in middleware instances. It never
creates a listener, service provider, shared framework, or process lifetime. That boundary keeps
the package usable by any Cohesion Web host and respects Cohesion's hosting-isolation rule.

The current feed makes this boundary concrete. `Assimalign.Cohesion.Sdk.Web` restores, but its
matching `App.Web.Ref` and `App.Web.Runtime.win-arm64` 10.0.1-preview.1 packs contain only the area
root rather than Web Hosting, StaticFiles, or Testing. The library therefore uses the packaged root
middleware seam and owns the small amount of required asset serving directly. Cohesion 10.0.2
annotates its public HTTP/Web API as preview, so this assembly must opt into and expose that preview
surface even though its implementation avoids runtime-preview APIs of its own.

## Manifest model and asset resolution

Both supported manifest object graphs are closed internal types registered on
`StaticWebAssetsJsonContext`. Metadata generation is chosen because the input is read once at
middleware construction; no reflection fallback is allowed. A malformed supplied manifest fails
composition with an `InvalidOperationException` instead of producing a partially valid catalog.

The runtime trie is flattened into exact route mappings and pattern registrations. Endpoint
variants remain grouped by public route so each request can select content encoding. Resolution is:

1. normalize and reject traversal or platform-separator input;
2. select an endpoint variant using client and manifest qualities;
3. resolve the endpoint's `AssetFile` through runtime roots/patterns or the configured content root;
4. if no endpoint resolves, try the normalized path directly;
5. for an otherwise unhandled non-file GET or HEAD outside `/_framework`, resolve identity
   `index.html` when fallback is enabled.

Every root combination is canonicalized and checked before opening a file. The catalog is immutable
after construction, while the managed-delta cache is the only synchronized mutable feature state.
Asset bodies stream from disk. Conditional validation and byte ranges are deferred in this first
package because the corresponding Cohesion StaticFiles implementation is not shipped in a
consumable package. Refresh injection intentionally buffers only HTML, removes stale
encoding/validator headers, recalculates content length, and forces `no-store`.

## Browser-refresh compatibility

The dotnet-watch middleware's client script is an embedded private resource and is not available as
a supported package API. The bridge vendors a readable minimum implementation of the observable
contract instead:

- a 500 ms delayed connection and one-page sentinel;
- comma-separated endpoint fallback;
- optional 32-byte shared secret encrypted with an SPKI RSA-OAEP/SHA-256 public key and sent as the
  URI-encoded WebSocket subprotocol;
- page reload/wait behavior, stylesheet cache busting, managed-delta acknowledgement, and HTTP
  delta caching.

The clear-cache endpoint returns `Clear-Site-Data: "cache"`. The hot-reload endpoint is terminal
while enabled: GET/POST follow the cache contract and all other verbs return 405. The script and
endpoints do not exist when the WebSocket endpoint environment variable is empty.

The first pass omits Microsoft's later diagnostics/capability messages, boot-resource response
header injection, and the managed-update log returned by Blazor's internal hook.
`ApplyManagedCodeUpdates` uses that hook when present; in a
pure Viu runtime it preserves the delta in the cache but cannot execute it until Viu exposes a
browser managed-update hook. Static CSS updates and full reloads are functional independently.

The asset catalog captures both manifests at middleware construction. Existing physical files can
change in place during watch, but newly fingerprinted routes require a process restart. This matches
the current dev-server lifecycle; an atomic manifest-reload layer remains a future enhancement if
the host begins surviving route-graph changes.

## Server-render adapter

`ViuServerRendererMiddleware` owns only host mapping. Application composition stays outside:

```text
Cohesion IHttpContext
  -> request root factory
  -> ServerRenderRequest<IHttpContext>
  -> ServerRenderAdaptor<IHttpContext>
  -> CohesionServerRenderOutput
  -> IHttpResponse.Body
```

The application supplies the adaptor because only it can create request services and invoke its
generated `GeneratedViuComponents.Register` and `GeneratedViuServerRenders.Register` methods. The
bridge avoids reflection, assembly scanning, service location, and hidden internal APIs.

The generated server-render registry is populated during application composition and frozen before
the adaptor is published to the request pipeline:

```csharp
ServerRenderRegistry registry = new();
GeneratedViuServerRenders.Register(registry);

IServerRenderRegistry serverRenders = registry.Freeze();
ServerRenderAdaptor<IHttpContext> adaptor =
    new(requestScopeFactory, serverRenders);
```

`Freeze` is an idempotent publication boundary. It publishes one immutable snapshot for lock-free
concurrent resolution and rejects every later registration, so all generated and application-owned
registrations must be complete before this transition.

Writes flow through one stateful UTF-8 encoder that leaves the borrowed response stream open.
This preserves surrogate pairs across Viu chunks and preserves exact awaited flush/backpressure
boundaries. Encoder completion observes `IHttpContext.RequestAborted` and occurs before a failed
buffered response is cleared, so final encoder bytes cannot be written after the replacement response
has truncated the stream. The adaptor receives that same request-abort token and passes it to
`IServerRenderRequestScope.DisposeAsync(CancellationToken)` on every teardown path.

Cohesion's transport remains authoritative over response commitment. While `IHttpResponse.Body` is
the current replaceable memory buffer, `CohesionServerRenderOutput.ResponseCommitted` is always
`false`: neither an attempted write nor an accepted flush makes the response irreversibly visible.
The middleware therefore replaces a failed buffered render with a clean 500 only when
`ServerRenderResult.ResponseCommitted` is false. When Cohesion gains a streaming transport, the
output property must instead delegate to its monotonic, transport-confirmed `HasStarted` state; a
failure after that state is true must preserve the committed stream. Viu snapshots the host's value
after request-scope teardown, so commitment remains authoritative even when teardown is the failing
operation.

The basic server-render overload emits the primary render stream. A document-shell overload also
accepts `Func<IHttpContext, IServerRenderDocumentShell>`, creates the shell per request, and calls
`ServerRenderAdaptor<IHttpContext>.RenderDocumentAsync`. Its ordering is strict:

1. the host-owned prefix is written and any non-empty pending content is flushed before component
   execution;
2. the main render retains Viu's progressive subtree flush and backpressure boundaries;
3. after a successful main render resolves every teleport, the host-owned suffix receives the
   stable target-to-payload map and is flushed before request-scope teardown.

Every teleport destination must occur in the suffix. The shell owns destination order and markers
and writes each matching payload verbatim because Viu already includes the required hydration
anchor. Prefix, render, output, or cancellation failure suppresses the suffix; suffix failure follows
the ordinary render-failure path.

This seam deliberately supports suffix-only teleport splicing. It cannot backpatch an already
streamed prefix, an already closed `<head>`, or an emitted main-body region. A host that needs an
earlier destination must buffer that document region or perform a render prepass.

## AOT constraints

Shipping code is marked AOT compatible. Request dispatch is ordinary closed-type code; manifest
JSON uses generated metadata; the refresh delta validator uses `JsonDocument`; and no reflection,
dynamic code generation, runtime XML parsing, or assembly discovery is used. Public registrations
and the supplied root factory keep application activation explicit.
