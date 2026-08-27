using System.Collections.Generic;

namespace Assimalign.Viu.Cohesion.Web.Internal;

internal sealed record StaticWebAssetDescriptor(
    string PhysicalPath,
    string LogicalPath,
    IReadOnlyDictionary<string, string> ResponseHeaders,
    string? ContentEncoding,
    bool IsEndpointAsset);
