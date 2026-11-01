using System;
using System.IO;

using Assimalign.Cohesion.Viu.Hosting;

namespace Assimalign.Cohesion.Viu.Server;

internal sealed record ViuApplicationConfiguration(
    string ContentRootPath,
    string? RuntimeManifestPath,
    string? EndpointsManifestPath,
    bool ApplyCrossOriginIsolationHeaders,
    BrowserRefreshConfiguration BrowserRefresh,
    bool EnableSpaFallback = true,
    string? ClientConfigurationPath = null,
    ViuClientConfiguration? ClientConfiguration = null)
{
    public static ViuApplicationConfiguration Create(ViuApplicationOptions options, string? environment = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ContentRootPath);
        if (options.ClientConfigurationPath is string route
            && (!route.StartsWith('/') || route.StartsWith("//", StringComparison.Ordinal)
                || route.Contains('?') || route.Contains('#') || !StaticWebAssetCatalog.IsSafeRequestPath(route)
                || HealthProbeRoutes.IsReserved(route)))
        {
            throw new ArgumentException("ClientConfigurationPath must be a local absolute route other than a health probe.", nameof(options));
        }

        string contentRootPath = Path.GetFullPath(options.ContentRootPath);
        string? runtimeManifestPath = NormalizeOptionalPath(options.RuntimeManifestPath);
        string? endpointsManifestPath = NormalizeOptionalPath(options.EndpointsManifestPath);

        return new ViuApplicationConfiguration(
            contentRootPath,
            runtimeManifestPath,
            endpointsManifestPath,
            options.ApplyCrossOriginIsolationHeaders,
            BrowserRefreshConfiguration.FromEnvironment(),
            options.EnableSpaFallback,
            options.ClientConfigurationPath,
            ClientConfigurationDocument.Create(environment ?? AppEnvironment.GetEnvironmentName(), options.ClientReferences));
    }

    private static string? NormalizeOptionalPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
    }
}
