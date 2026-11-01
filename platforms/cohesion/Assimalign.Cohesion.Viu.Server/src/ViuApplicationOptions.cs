using System;
using System.Collections.Generic;
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
    private readonly Dictionary<string, Dictionary<string, string>> _clientReferences = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the public client document route. Set to null to disable it.</summary>
    public string? ClientConfigurationPath { get; set; } = "/_cohesion/client.json";

    /// <summary>
    /// Publishes an endpoint the browser client calls in the client document, under
    /// <c>references[resource][endpoint]</c>.
    /// </summary>
    /// <param name="resource">The name the client looks the endpoint up by, such as <c>api</c>.</param>
    /// <param name="endpoint">The endpoint's name, such as <c>http</c>.</param>
    /// <param name="url">The endpoint's absolute URL.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="resource"/> or <paramref name="endpoint"/> is empty, or <paramref name="url"/>
    /// is not an endpoint URL (absolute, with a host and an explicit port, and no user information,
    /// query or fragment).
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="url"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The document publishes only what the application adds here. A gateway passes the address of a
    /// resource this one depends on as a setting, which the application reads from its configuration,
    /// for example
    /// <c>viu.AddClientReference("api", "http", new Uri(builder.Configuration["Api:Url"]!))</c>.
    /// A later call for the same resource and endpoint replaces the earlier URL.
    /// </remarks>
    public void AddClientReference(string resource, string endpoint, Uri url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        Uri.ThrowIfNotEndpoint(url);
        if (!_clientReferences.TryGetValue(resource, out Dictionary<string, string>? endpoints))
        {
            endpoints = new Dictionary<string, string>(StringComparer.Ordinal);
            _clientReferences.Add(resource, endpoints);
        }
        endpoints[endpoint] = url.ToEndpointString();
    }

    /// <summary>Gets a snapshot of the references the application added, grouped by resource.</summary>
    internal IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ClientReferences
    {
        get
        {
            Dictionary<string, IReadOnlyDictionary<string, string>> snapshot = new(StringComparer.Ordinal);
            foreach ((string resource, Dictionary<string, string> endpoints) in _clientReferences)
            {
                snapshot.Add(resource, new Dictionary<string, string>(endpoints, StringComparer.Ordinal));
            }
            return snapshot;
        }
    }

    /// <summary>Selects the published physical root, or the client build output and its asset manifests.</summary>
    /// <param name="clientAssemblyName">The assembly identity of the marked ViuClient ProjectReference.</param>
    /// <exception cref="InvalidOperationException">
    /// No published root exists and no <c>&lt;client&gt;.viu-client.json</c> document for the client
    /// exists in the content root the platform names or beside the executable.
    /// </exception>
    /// <remarks>
    /// The Server build targets write <c>&lt;client&gt;.viu-client.json</c>, naming the client and its
    /// build output directory, as copy-to-output content of the Web executable. It is read from the
    /// content root the instance's platform names (<see cref="AppEnvironment.Variables.ContentRoot"/>,
    /// which the runtime contract reserves and no current gateway sets), when it names one, and from
    /// beside the executable. A gateway that runs the Web program in its own process references the
    /// Web project, so the document is copied beside the gateway executable; each client's document
    /// has its own name there, so name the client when one gateway runs several Viu Web projects.
    /// Neither location depends on the process-wide runtime configuration.
    /// </remarks>
    public void UseClientOutput(string clientAssemblyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientAssemblyName);
        UseClientOutputCore(clientAssemblyName);
    }

    /// <summary>Selects the sole marked client's document, or the published physical root.</summary>
    /// <exception cref="InvalidOperationException">
    /// No published root and no client document exists, or the directory searched holds documents
    /// for more than one client (a gateway that runs several Viu Web projects); call
    /// <see cref="UseClientOutput(string)"/> with the client's name instead.
    /// </exception>
    public void UseClientOutput() => UseClientOutputCore(null);

    private void UseClientOutputCore(string? clientAssemblyName)
    {
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
            if (!ViuClientDocument.TryRead(directory, clientAssemblyName, out ViuClientDocument? document))
            {
                continue;
            }
            ContentRootPath = Path.Combine(document.OutputPath!, "wwwroot");
            RuntimeManifestPath = document.RuntimeManifestPath;
            EndpointsManifestPath = document.EndpointsManifestPath;
            return;
        }

        throw new InvalidOperationException(
            $"No '{(clientAssemblyName is null ? "*" + ViuClientDocument.FileSuffix : ViuClientDocument.FileName(clientAssemblyName))}' document for client '{clientAssemblyName ?? "(inferred)"}' exists in the content root or beside the executable. Declare a ProjectReference with ViuClient=\"true\" on the Web executable and build it, or publish its wwwroot.");
    }

    private static IEnumerable<string> ClientDocumentDirectories()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string? contentRoot = AppEnvironment.GetValue(AppEnvironment.Variables.ContentRoot);
        if (string.IsNullOrWhiteSpace(contentRoot))
        {
            yield return baseDirectory;
            yield break;
        }

        yield return contentRoot;
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
    /// Gets or sets a value indicating whether cross-origin isolation response headers are added.
    /// </summary>
    /// <remarks>
    /// Enable this for WebAssembly threads. Every response handled by the middleware receives
    /// <c>Cross-Origin-Embedder-Policy: require-corp</c> and
    /// <c>Cross-Origin-Opener-Policy: same-origin</c>.
    /// </remarks>
    public bool ApplyCrossOriginIsolationHeaders { get; set; }
}
