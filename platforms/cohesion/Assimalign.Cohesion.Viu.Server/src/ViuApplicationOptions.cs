using System;
using System.IO;

namespace Assimalign.Cohesion.Viu.Server;

/// <summary>
/// Configures the files and response policy used to host a Viu browser application.
/// </summary>
/// <remarks>
/// Paths are captured when <c>AddViuApplication</c> registers the application or when the
/// pipeline-only <c>UseViuApplication</c> overload composes the middleware. The resulting
/// middleware performs no service lookup and does not mutate these options at request time.
/// </remarks>
public sealed class ViuApplicationOptions
{
    /// <summary>
    /// Gets or sets the physical application content root, normally the build output's
    /// <c>wwwroot</c> directory.
    /// </summary>
    public string ContentRootPath { get; set; } = Path.Combine(Environment.CurrentDirectory, "wwwroot");

    /// <summary>
    /// Gets or sets the optional static-web-assets runtime manifest path.
    /// </summary>
    public string? RuntimeManifestPath { get; set; }

    /// <summary>
    /// Gets or sets the optional static-web-assets endpoints manifest path.
    /// </summary>
    public string? EndpointsManifestPath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an otherwise unhandled GET or HEAD request is
    /// answered with <c>index.html</c>.
    /// </summary>
    /// <remarks>
    /// Disable this when static assets run before <c>UseViuServerRenderer</c>, allowing the server
    /// renderer or another downstream route handler to own application documents.
    /// </remarks>
    public bool EnableSpaFallback { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether cross-origin isolation response headers are added.
    /// </summary>
    /// <remarks>
    /// Enable this for WebAssembly threads. Every response handled by the middleware receives
    /// <c>Cross-Origin-Embedder-Policy: require-corp</c> and
    /// <c>Cross-Origin-Opener-Policy: same-origin</c>.
    /// </remarks>
    public bool ApplyCrossOriginIsolationHeaders { get; set; }
}
