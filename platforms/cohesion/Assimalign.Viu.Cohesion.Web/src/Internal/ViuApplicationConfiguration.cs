using System;
using System.IO;

namespace Assimalign.Viu.Cohesion.Web.Internal;

internal sealed record ViuApplicationConfiguration(
    string ContentRootPath,
    string? RuntimeManifestPath,
    string? EndpointsManifestPath,
    bool ApplyCrossOriginIsolationHeaders,
    BrowserRefreshConfiguration BrowserRefresh,
    bool EnableSpaFallback = true)
{
    public static ViuApplicationConfiguration Create(ViuApplicationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ContentRootPath);

        string contentRootPath = Path.GetFullPath(options.ContentRootPath);
        string? runtimeManifestPath = NormalizeOptionalPath(options.RuntimeManifestPath);
        string? endpointsManifestPath = NormalizeOptionalPath(options.EndpointsManifestPath);

        return new ViuApplicationConfiguration(
            contentRootPath,
            runtimeManifestPath,
            endpointsManifestPath,
            options.ApplyCrossOriginIsolationHeaders,
            BrowserRefreshConfiguration.FromEnvironment(),
            options.EnableSpaFallback);
    }

    private static string? NormalizeOptionalPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
    }
}
