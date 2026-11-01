using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Assimalign.Cohesion.Viu.Server;

/// <summary>
/// The build-produced <c>&lt;client&gt;.viu-client.json</c> document that names the browser client
/// assembly and its build output directory. The Server build targets write it into the Web
/// executable's intermediate directory and declare it as copy-to-output content, so it travels
/// beside the executable, and beside a gateway executable that references the Web project to run it
/// in its own process. The file is named for its client, so the documents of several Web projects
/// one gateway references never overwrite each other.
/// </summary>
internal sealed record ViuClientDocument
{
    /// <summary>The suffix every client document's file name ends with.</summary>
    internal const string FileSuffix = ".viu-client.json";

    /// <summary>Gets the document's file name for <paramref name="clientAssemblyName"/>.</summary>
    internal static string FileName(string clientAssemblyName) => clientAssemblyName + FileSuffix;

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

    /// <summary>Reads the document in <paramref name="directory"/> for the requested client.</summary>
    /// <param name="directory">The directory that may hold the document.</param>
    /// <param name="clientAssemblyName">
    /// The client assembly the caller hosts, or <see langword="null"/> to infer the directory's sole
    /// client document.
    /// </param>
    /// <param name="clientDocument">The selected document when found.</param>
    /// <returns><see langword="true"/> when a document for the client exists.</returns>
    /// <exception cref="InvalidOperationException">
    /// The document exists but is not valid JSON or is incomplete, or no client is named and the
    /// directory holds documents for more than one client.
    /// </exception>
    internal static bool TryRead(
        string directory,
        string? clientAssemblyName,
        [NotNullWhen(true)] out ViuClientDocument? clientDocument)
    {
        clientDocument = null;
        string path;
        if (clientAssemblyName is not null)
        {
            path = Path.Combine(directory, FileName(clientAssemblyName));
            if (!File.Exists(path))
            {
                return false;
            }
        }
        else
        {
            if (!Directory.Exists(directory))
            {
                return false;
            }
            List<string> candidates = Directory.EnumerateFiles(directory)
                .Where(static candidate =>
                {
                    string name = Path.GetFileName(candidate);
                    return name.Length > FileSuffix.Length && name.EndsWith(FileSuffix, StringComparison.OrdinalIgnoreCase);
                })
                .Order(StringComparer.Ordinal)
                .ToList();
            if (candidates.Count == 0)
            {
                return false;
            }
            if (candidates.Count > 1)
            {
                throw new InvalidOperationException(
                    $"'{directory}' holds Viu client documents for more than one client ({string.Join(", ", candidates.Select(Path.GetFileName))}), as a gateway that runs several Viu Web projects does. Name the client with UseClientOutput(\"<client assembly>\").");
            }
            path = candidates[0];
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
