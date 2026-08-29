using System;
using System.Collections.Generic;
using System.IO;

using Assimalign.Cohesion.FileSystem.Globbing;

namespace Assimalign.Cohesion.Viu.Server;

internal sealed class StaticWebAssetCatalog
{
    private static readonly StringComparer EndpointPathComparer = StringComparer.Ordinal;
    private static readonly StringComparer FileSystemRouteComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
    private static readonly StringComparison FileSystemPathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
    private static readonly StringComparison FileSystemRouteComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private readonly string _contentRootPath;
    private readonly Dictionary<string, string> _runtimeAssets = new(FileSystemRouteComparer);
    private readonly List<StaticWebAssetPatternRegistration> _runtimePatterns = [];
    private readonly Dictionary<string, List<StaticWebAssetsEndpoint>> _endpoints = new(EndpointPathComparer);

    private StaticWebAssetCatalog(string contentRootPath)
    {
        _contentRootPath = contentRootPath;
    }

    public static StaticWebAssetCatalog Load(ViuApplicationConfiguration configuration)
    {
        StaticWebAssetsRuntimeManifest? runtime = StaticWebAssetsManifestReader.ReadRuntime(configuration.RuntimeManifestPath);
        StaticWebAssetsEndpointsManifest? endpoints = StaticWebAssetsManifestReader.ReadEndpoints(configuration.EndpointsManifestPath);

        StaticWebAssetCatalog catalog = new(configuration.ContentRootPath);
        if (runtime is not null)
        {
            catalog.AddRuntimeNode(runtime, runtime.Root, string.Empty);
        }
        if (endpoints is not null)
        {
            catalog.AddEndpoints(endpoints);
        }

        return catalog;
    }

    public StaticWebAssetDescriptor? Resolve(string requestPath, string? acceptEncoding, bool requireIdentity = false)
    {
        string? route = NormalizeRoute(requestPath);
        if (route is null)
        {
            return null;
        }

        if (_endpoints.TryGetValue(route, out List<StaticWebAssetsEndpoint>? variants))
        {
            StaticWebAssetsEndpoint? endpoint = SelectEndpoint(variants, acceptEncoding, requireIdentity);
            if (endpoint is not null && TryResolvePhysicalPath(endpoint.AssetFile, out string? physicalPath))
            {
                Dictionary<string, string> responseHeaders = new(StringComparer.OrdinalIgnoreCase);
                foreach (StaticWebAssetsNameValue header in endpoint.ResponseHeaders)
                {
                    responseHeaders[header.Name] = header.Value;
                }

                responseHeaders.TryGetValue("Content-Encoding", out string? contentEncoding);
                return new StaticWebAssetDescriptor(
                    physicalPath,
                    endpoint.AssetFile,
                    responseHeaders,
                    contentEncoding,
                    IsEndpointAsset: true);
            }
        }

        if (TryResolvePhysicalPath(route, out string? resolvedPath))
        {
            return new StaticWebAssetDescriptor(
                resolvedPath,
                route,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                ContentEncoding: null,
                IsEndpointAsset: false);
        }

        return null;
    }

    public static bool IsSafeRequestPath(string requestPath) => NormalizeRoute(requestPath) is not null;

    private void AddRuntimeNode(
        StaticWebAssetsRuntimeManifest manifest,
        StaticWebAssetsRuntimeNode node,
        string routePrefix)
    {
        if (node.Asset is StaticWebAssetsRuntimeAsset asset
            && TryGetContentRoot(manifest, asset.ContentRootIndex, out string? assetContentRoot))
        {
            string? physicalPath = CombineWithinRoot(assetContentRoot, asset.SubPath);
            if (physicalPath is not null)
            {
                _runtimeAssets[routePrefix] = physicalPath;
            }
        }

        if (node.Patterns is not null)
        {
            foreach (StaticWebAssetsRuntimePattern pattern in node.Patterns)
            {
                if (TryGetContentRoot(manifest, pattern.ContentRootIndex, out string? patternContentRoot))
                {
                    GlobMatcherBuilder matcherBuilder = new(new GlobMatcherOptions
                    {
                        IgnoreCase = OperatingSystem.IsWindows(),
                    });
                    IGlobMatcher matcher = matcherBuilder
                        .AddInclude(Glob.Parse(pattern.Pattern))
                        .Build();
                    _runtimePatterns.Add(new StaticWebAssetPatternRegistration(
                        routePrefix,
                        patternContentRoot,
                        pattern.Depth,
                        matcher));
                }
            }
        }

        if (node.Children is null)
        {
            return;
        }

        foreach ((string name, StaticWebAssetsRuntimeNode child) in node.Children)
        {
            string childRoute = routePrefix.Length == 0 ? name : routePrefix + "/" + name;
            AddRuntimeNode(manifest, child, childRoute);
        }
    }

    private void AddEndpoints(StaticWebAssetsEndpointsManifest manifest)
    {
        foreach (StaticWebAssetsEndpoint endpoint in manifest.Endpoints)
        {
            string? route = NormalizeRoute(endpoint.Route);
            if (route is null)
            {
                continue;
            }

            if (!_endpoints.TryGetValue(route, out List<StaticWebAssetsEndpoint>? variants))
            {
                variants = [];
                _endpoints.Add(route, variants);
            }
            variants.Add(endpoint);
        }
    }

    private bool TryResolvePhysicalPath(string logicalPath, out string physicalPath)
    {
        string? route = NormalizeRoute(logicalPath);
        if (route is null)
        {
            physicalPath = string.Empty;
            return false;
        }

        if (_runtimeAssets.TryGetValue(route, out string? exactPath) && File.Exists(exactPath))
        {
            physicalPath = exactPath;
            return true;
        }

        foreach (StaticWebAssetPatternRegistration pattern in _runtimePatterns)
        {
            if (TryResolvePattern(pattern, route, out physicalPath))
            {
                return true;
            }
        }

        string? contentPath = CombineWithinRoot(_contentRootPath, route);
        if (contentPath is not null && File.Exists(contentPath))
        {
            physicalPath = contentPath;
            return true;
        }

        physicalPath = string.Empty;
        return false;
    }

    private static StaticWebAssetsEndpoint? SelectEndpoint(
        List<StaticWebAssetsEndpoint> variants,
        string? acceptEncoding,
        bool requireIdentity)
    {
        StaticWebAssetsEndpoint? identity = null;
        (StaticWebAssetsEndpoint Endpoint, double Quality)? brotli = null;
        (StaticWebAssetsEndpoint Endpoint, double Quality)? gzip = null;

        foreach (StaticWebAssetsEndpoint endpoint in variants)
        {
            string? encoding = GetSelectedEncoding(endpoint);
            switch (encoding?.ToLowerInvariant())
            {
                case "br":
                    brotli = SelectHigherQuality(brotli, endpoint);
                    break;
                case "gzip":
                    gzip = SelectHigherQuality(gzip, endpoint);
                    break;
                case null:
                    identity ??= endpoint;
                    break;
            }
        }

        if (requireIdentity)
        {
            return identity;
        }

        string[] serverCodings = GetServerCodings(brotli, gzip);
        string selected = AcceptEncoding.Select(acceptEncoding, serverCodings);
        if (selected.Equals("identity", StringComparison.Ordinal))
        {
            return identity;
        }

        if (selected.Equals("br", StringComparison.OrdinalIgnoreCase))
        {
            return brotli?.Endpoint ?? identity;
        }

        return selected.Equals("gzip", StringComparison.OrdinalIgnoreCase)
            ? gzip?.Endpoint ?? identity
            : identity;
    }

    private static string[] GetServerCodings(
        (StaticWebAssetsEndpoint Endpoint, double Quality)? brotli,
        (StaticWebAssetsEndpoint Endpoint, double Quality)? gzip)
    {
        if (brotli is null)
        {
            return gzip is null ? [] : ["gzip"];
        }
        if (gzip is null)
        {
            return ["br"];
        }

        return brotli.Value.Quality >= gzip.Value.Quality
            ? ["br", "gzip"]
            : ["gzip", "br"];
    }

    private static (StaticWebAssetsEndpoint Endpoint, double Quality) SelectHigherQuality(
        (StaticWebAssetsEndpoint Endpoint, double Quality)? current,
        StaticWebAssetsEndpoint candidate)
    {
        double quality = GetSelectorQuality(candidate);
        return current is null || quality > current.Value.Quality
            ? (candidate, quality)
            : current.Value;
    }

    private static double GetSelectorQuality(StaticWebAssetsEndpoint endpoint)
    {
        foreach (StaticWebAssetsSelector selector in endpoint.Selectors)
        {
            if (selector.Name.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase)
                && double.TryParse(
                    selector.Quality,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double quality))
            {
                return quality;
            }
        }

        return 1;
    }

    private static string? GetSelectedEncoding(StaticWebAssetsEndpoint endpoint)
    {
        foreach (StaticWebAssetsSelector selector in endpoint.Selectors)
        {
            if (selector.Name.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase))
            {
                return selector.Value;
            }
        }
        return null;
    }

    private static bool TryResolvePattern(
        StaticWebAssetPatternRegistration pattern,
        string route,
        out string physicalPath)
    {
        string prefix = pattern.RoutePrefix;
        if (prefix.Length > 0
            && !route.Equals(prefix, FileSystemRouteComparison)
            && !route.StartsWith(prefix + "/", FileSystemRouteComparison))
        {
            physicalPath = string.Empty;
            return false;
        }

        string[] segments = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pattern.Depth < 0 || segments.Length < pattern.Depth)
        {
            physicalPath = string.Empty;
            return false;
        }

        string remainder = string.Join('/', segments[pattern.Depth..]);
        if (!pattern.Matcher.IsMatch(FileSystemPath.Parse(remainder)))
        {
            physicalPath = string.Empty;
            return false;
        }

        string? candidate = CombineWithinRoot(pattern.ContentRoot, remainder);
        if (candidate is not null && File.Exists(candidate))
        {
            physicalPath = candidate;
            return true;
        }

        physicalPath = string.Empty;
        return false;
    }

    private static bool TryGetContentRoot(
        StaticWebAssetsRuntimeManifest manifest,
        int index,
        out string contentRoot)
    {
        if ((uint)index < (uint)manifest.ContentRoots.Length)
        {
            contentRoot = Path.GetFullPath(manifest.ContentRoots[index]);
            return true;
        }

        contentRoot = string.Empty;
        return false;
    }

    private static string? NormalizeRoute(string path)
    {
        string route = path.TrimStart('/');
        if (route.Contains('\\')
            || route.Contains('\0')
            || route.Contains(':'))
        {
            return null;
        }

        string[] segments = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (string segment in segments)
        {
            if (segment is "." or "..")
            {
                return null;
            }
        }

        string normalized = string.Join('/', segments);
        if (normalized.Length > 0)
        {
            try
            {
                _ = FileSystemPath.Parse(normalized);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        return normalized;
    }

    private static string? CombineWithinRoot(string root, string relativePath)
    {
        string fullRoot = Path.GetFullPath(root);
        string rootPrefix = Path.EndsInDirectorySeparator(fullRoot)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;
        string platformPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        string candidate = Path.GetFullPath(Path.Combine(fullRoot, platformPath));

        if (!candidate.Equals(fullRoot, FileSystemPathComparison)
            && !candidate.StartsWith(rootPrefix, FileSystemPathComparison))
        {
            return null;
        }

        return candidate;
    }

    private sealed record StaticWebAssetPatternRegistration(
        string RoutePrefix,
        string ContentRoot,
        int Depth,
        IGlobMatcher Matcher);
}
