using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Assimalign.Cohesion.Viu.Server;

/// <summary>
/// The build-produced <c>viu-client.json</c> document that names the browser client assembly and
/// its build output directory. The Server build targets write it into the Web executable's
/// intermediate directory and declare it as copy-to-output content, so it travels beside the
/// executable and, for an in-process member, inside the resource content root the gateway stages.
/// </summary>
internal sealed record ViuClientDocument
{
    internal const string FileName = "viu-client.json";

    /// <summary>Gets the Browser SDK's client assembly identity.</summary>
    [JsonPropertyName("client")]
    public string? Client { get; init; }

    /// <summary>Gets the client's evaluated build output directory.</summary>
    [JsonPropertyName("outputPath")]
    public string? OutputPath { get; init; }

    /// <summary>Gets the runtime manifest path returned by the Browser SDK.</summary>
    [JsonPropertyName("runtimeManifestPath")]
    public string? RuntimeManifestPath { get; init; }

    /// <summary>Gets the endpoints manifest path returned by the Browser SDK.</summary>
    [JsonPropertyName("endpointsManifestPath")]
    public string? EndpointsManifestPath { get; init; }

    /// <summary>Reads the document in <paramref name="directory"/> when it names the requested client.</summary>
    /// <param name="directory">The directory that may hold the document.</param>
    /// <param name="clientAssemblyName">The client assembly the caller hosts.</param>
    /// <param name="clientDocument">The selected document when found.</param>
    /// <returns><see langword="true"/> when a document for the client exists.</returns>
    /// <exception cref="InvalidOperationException">The document exists but is not valid JSON.</exception>
    internal static bool TryRead(
        string directory,
        string? clientAssemblyName,
        [NotNullWhen(true)] out ViuClientDocument? clientDocument)
    {
        clientDocument = null;
        string path = Path.Combine(directory, FileName);
        if (!File.Exists(path))
        {
            return false;
        }

        ViuClientDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(File.ReadAllBytes(path), StaticWebAssetsJsonContext.Default.ViuClientDocument);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"The Viu client document '{path}' is not valid JSON.", exception);
        }

        if (document is null
            || (clientAssemblyName is not null && !string.Equals(document.Client, clientAssemblyName, StringComparison.Ordinal)))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(document.Client) || string.IsNullOrWhiteSpace(document.OutputPath)
            || string.IsNullOrWhiteSpace(document.RuntimeManifestPath) || string.IsNullOrWhiteSpace(document.EndpointsManifestPath))
        {
            throw new InvalidOperationException($"The Viu client document '{path}' is incomplete. Rebuild the Web project with a ProjectReference marked ViuClient=\"true\".");
        }
        clientDocument = document;
        return true;
    }
}
