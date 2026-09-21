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
    ViuClientConfiguration? ClientConfiguration = null,
    string ControlPlanePath = ResourceRoutes.DefaultControlPlanePath)
{
    public static ViuApplicationConfiguration Create(ViuApplicationOptions options, string? environment = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ContentRootPath);
        string controlPlanePath = ResourceRoutes.NormalizeControlPlanePath(options.ControlPlanePath);
        if (options.ClientConfigurationPath is string route
            && (!route.StartsWith('/') || route.StartsWith("//", StringComparison.Ordinal)
                || route.Contains('?') || route.Contains('#') || !StaticWebAssetCatalog.IsSafeRequestPath(route)
                || ResourceRoutes.IsReserved(route, controlPlanePath)))
        {
            throw new ArgumentException("ClientConfigurationPath must be a local absolute route outside the resource control plane.", nameof(options));
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
            ClientConfigurationDocument.Create(environment ?? AppEnvironment.GetEnvironmentName()),
            controlPlanePath);
    }

    private static string? NormalizeOptionalPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
    }
}
