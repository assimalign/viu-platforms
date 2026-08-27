namespace Assimalign.Viu.Cohesion.Web.Internal;

internal sealed class StaticWebAssetsEndpointsManifest
{
    public int Version { get; init; }

    public string ManifestType { get; init; } = string.Empty;

    public StaticWebAssetsEndpoint[] Endpoints { get; init; } = [];
}

internal sealed class StaticWebAssetsEndpoint
{
    public string Route { get; init; } = string.Empty;

    public string AssetFile { get; init; } = string.Empty;

    public StaticWebAssetsSelector[] Selectors { get; init; } = [];

    public StaticWebAssetsNameValue[] ResponseHeaders { get; init; } = [];

    public StaticWebAssetsNameValue[] EndpointProperties { get; init; } = [];
}

internal sealed class StaticWebAssetsSelector
{
    public string Name { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string Quality { get; init; } = string.Empty;
}

internal sealed class StaticWebAssetsNameValue
{
    public string Name { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;
}
