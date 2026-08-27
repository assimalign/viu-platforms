using System.Collections.Generic;

namespace Assimalign.Viu.Cohesion.Web.Internal;

internal sealed class StaticWebAssetsRuntimeManifest
{
    public string[] ContentRoots { get; init; } = [];

    public StaticWebAssetsRuntimeNode Root { get; init; } = new();
}

internal sealed class StaticWebAssetsRuntimeNode
{
    public Dictionary<string, StaticWebAssetsRuntimeNode>? Children { get; init; }

    public StaticWebAssetsRuntimeAsset? Asset { get; init; }

    public StaticWebAssetsRuntimePattern[]? Patterns { get; init; }
}

internal sealed class StaticWebAssetsRuntimeAsset
{
    public int ContentRootIndex { get; init; }

    public string SubPath { get; init; } = string.Empty;
}

internal sealed class StaticWebAssetsRuntimePattern
{
    public int ContentRootIndex { get; init; }

    public string Pattern { get; init; } = string.Empty;

    public int Depth { get; init; }
}
