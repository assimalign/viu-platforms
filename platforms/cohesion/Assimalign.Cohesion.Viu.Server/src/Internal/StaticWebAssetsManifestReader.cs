using System;
using System.IO;
using System.Text.Json;

namespace Assimalign.Cohesion.Viu.Server;

internal static class StaticWebAssetsManifestReader
{
    public static StaticWebAssetsRuntimeManifest? ReadRuntime(string? path)
    {
        if (path is null)
        {
            return null;
        }

        return Read(
            path,
            StaticWebAssetsJsonContext.Default.StaticWebAssetsRuntimeManifest,
            "static-web-assets runtime");
    }

    public static StaticWebAssetsEndpointsManifest? ReadEndpoints(string? path)
    {
        if (path is null)
        {
            return null;
        }

        return Read(
            path,
            StaticWebAssetsJsonContext.Default.StaticWebAssetsEndpointsManifest,
            "static-web-assets endpoints");
    }

    private static T Read<T>(string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, string manifestKind)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            T? manifest = JsonSerializer.Deserialize(stream, typeInfo);
            return manifest ?? throw new InvalidOperationException(
                $"The {manifestKind} manifest '{path}' contained no JSON value.");
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException(
                $"The {manifestKind} manifest '{path}' could not be read.",
                exception);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"The {manifestKind} manifest '{path}' is not valid JSON for the expected schema.",
                exception);
        }
    }
}
