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

    /// <summary>Gets the client assembly name declared by <c>ViuClientProject</c>.</summary>
    [JsonPropertyName("client")]
    public string? Client { get; init; }

    /// <summary>Gets the client's evaluated build output directory.</summary>
    [JsonPropertyName("outputPath")]
    public string? OutputPath { get; init; }

    /// <summary>Reads the document in <paramref name="directory"/> when it names the requested client.</summary>
    /// <param name="directory">The directory that may hold the document.</param>
    /// <param name="clientAssemblyName">The client assembly the caller hosts.</param>
    /// <param name="outputPath">The client build output directory when found.</param>
    /// <returns><see langword="true"/> when a document for the client exists.</returns>
    /// <exception cref="InvalidOperationException">The document exists but is not valid JSON.</exception>
    internal static bool TryRead(
        string directory,
        string clientAssemblyName,
        [NotNullWhen(true)] out string? outputPath)
    {
        outputPath = null;
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
            || !string.Equals(document.Client, clientAssemblyName, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(document.OutputPath))
        {
            return false;
        }

        outputPath = document.OutputPath;
        return true;
    }
}
