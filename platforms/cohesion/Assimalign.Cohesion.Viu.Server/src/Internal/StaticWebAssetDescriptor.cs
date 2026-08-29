using System.Collections.Generic;

namespace Assimalign.Cohesion.Viu.Server;

internal sealed record StaticWebAssetDescriptor(
    string PhysicalPath,
    string LogicalPath,
    IReadOnlyDictionary<string, string> ResponseHeaders,
    string? ContentEncoding,
    bool IsEndpointAsset);
