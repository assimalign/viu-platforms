using System;

using Assimalign.Cohesion.ApplicationModel;

namespace Assimalign.Cohesion.Viu.ApplicationModel;

/// <summary>Names and reads the Web manifest properties emitted for a Viu client project.</summary>
public static class ViuWebManifestProperties
{
    /// <summary>The client assembly-name property.</summary>
    public const string Client = "web.viu.client";

    /// <summary>The SPA fallback property.</summary>
    public const string SpaFallback = "web.viu.spa-fallback";

    /// <summary>The site base-path property.</summary>
    public const string BasePath = "web.viu.base-path";

    /// <summary>Reads and validates the three Viu properties without changing the manifest.</summary>
    /// <param name="manifest">The resource manifest.</param>
    /// <param name="clientAssembly">The declared client assembly or an empty string.</param>
    /// <param name="spaFallback">The parsed fallback flag or false.</param>
    /// <param name="basePath">The declared base path or an empty string.</param>
    /// <returns>True when every property is valid.</returns>
    public static bool TryRead(ResourceManifest manifest, out string clientAssembly, out bool spaFallback, out string basePath)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        clientAssembly = manifest.Properties.TryGetValue(Client, out string? client) ? client ?? string.Empty : string.Empty;
        basePath = manifest.Properties.TryGetValue(BasePath, out string? path) ? path ?? string.Empty : string.Empty;
        bool validFlag = manifest.Properties.TryGetValue(SpaFallback, out string? flag) && bool.TryParse(flag, out _);
        spaFallback = bool.TryParse(flag, out bool parsed) && parsed;
        return IsClientValid(clientAssembly) && validFlag && IsBasePathValid(basePath);
    }

    internal static bool IsClientValid(string value) => !string.IsNullOrWhiteSpace(value)
        && value.IndexOfAny(['/', '\\', ':', '\0']) < 0;

    internal static bool IsBasePathValid(string value) => value.StartsWith('/')
        && !value.StartsWith("//", StringComparison.Ordinal)
        && value.IndexOfAny(['?', '#', '\\', '\0']) < 0
        && Array.TrueForAll(value.Split('/'), static segment => segment is not "." and not "..");

    internal static void Validate(ResourceManifest manifest)
    {
        _ = TryRead(manifest, out string client, out _, out string path);
        if (!IsClientValid(client)) { ThrowInvalid(Client); }
        if (!manifest.Properties.TryGetValue(SpaFallback, out string? flag) || !bool.TryParse(flag, out _)) { ThrowInvalid(SpaFallback); }
        if (!IsBasePathValid(path)) { ThrowInvalid(BasePath); }
    }

    private static void ThrowInvalid(string property) => throw new ArgumentException(
        $"Viu Web manifest property '{property}' is missing or invalid. Set ViuClientProject on the Web executable and rebuild its resource.json.", "manifest");
}
