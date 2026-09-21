using System;
using System.Collections.Generic;
using System.IO;

using Assimalign.Cohesion.Hosting.Resources;

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
    /// <summary>Gets or sets the public client document route. Set to null to disable it.</summary>
    public string? ClientConfigurationPath { get; set; } = "/_cohesion/client.json";

    /// <summary>Selects the published physical root, or the client build output and its asset manifests.</summary>
    /// <param name="clientAssemblyName">The client assembly name declared by the ViuClientProject project.</param>
    /// <exception cref="InvalidOperationException">
    /// No published root exists and no <c>viu-client.json</c> document for the client exists in the
    /// ambient resource content root or beside the executable.
    /// </exception>
    /// <remarks>
    /// The Server build targets write <c>viu-client.json</c>, naming the client and its build output
    /// directory, as copy-to-output content of the Web executable. Out of process it sits beside the
    /// executable; hosted in a gateway process it is staged into the member's resource content root,
    /// which the ambient <see cref="ResourceContext"/> names. Neither location depends on the
    /// process-wide runtime configuration.
    /// </remarks>
    public void UseClientOutput(string clientAssemblyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientAssemblyName);
        string publishedRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        if (File.Exists(Path.Combine(publishedRoot, "index.html")))
        {
            ContentRootPath = publishedRoot;
            RuntimeManifestPath = null;
            EndpointsManifestPath = null;
            return;
        }

        foreach (string directory in ClientDocumentDirectories())
        {
            if (!ViuClientDocument.TryRead(directory, clientAssemblyName, out string? output))
            {
                continue;
            }
            ContentRootPath = Path.Combine(output, "wwwroot");
            RuntimeManifestPath = Path.Combine(output, clientAssemblyName + ".staticwebassets.runtime.json");
            EndpointsManifestPath = Path.Combine(output, clientAssemblyName + ".staticwebassets.endpoints.json");
            return;
        }

        throw new InvalidOperationException(
            $"No '{ViuClientDocument.FileName}' document for client '{clientAssemblyName}' exists in the resource content root or beside the executable. Set ViuClientProject on the Web executable and build it, or publish its wwwroot.");
    }

    private static IEnumerable<string> ClientDocumentDirectories()
    {
        string contentRoot = ResourceRuntime.Current.ContentRootPath;
        yield return contentRoot;
        string baseDirectory = AppContext.BaseDirectory;
        if (!string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(contentRoot)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDirectory)),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            yield return baseDirectory;
        }
    }

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
    /// Gets or sets the resource control-plane path the Viu middleware never answers, so the
    /// orchestrated Web host's terminal routes are reached even when the fallback is enabled.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>/cohesion/v1</c>, the path <c>Assimalign.Cohesion.Sdk.Web</c> declares. Set it
    /// when the resource declares a different <c>CohesionControlPlanePath</c>. Only this path and its
    /// subtree plus the bare <c>/healthz</c>, <c>/readyz</c>, and <c>/livez</c> probes are reserved;
    /// an application route such as <c>/cohesion</c> or <c>/cohesion/database</c> is served normally.
    /// </remarks>
    public string ControlPlanePath { get; set; } = ResourceRoutes.DefaultControlPlanePath;

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
