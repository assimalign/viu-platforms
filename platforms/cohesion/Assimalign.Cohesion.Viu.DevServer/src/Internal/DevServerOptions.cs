namespace Assimalign.Cohesion.Viu.DevServer;

internal sealed record DevServerOptions(
    string ContentRootPath,
    string EndpointsManifestPath,
    string RuntimeManifestPath,
    bool ApplyCrossOriginIsolationHeaders,
    string? Urls);
