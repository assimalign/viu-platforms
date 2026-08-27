using System.Text.Json.Serialization;

namespace Assimalign.Viu.Cohesion.Web.Internal;

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNameCaseInsensitive = false)]
[JsonSerializable(typeof(StaticWebAssetsRuntimeManifest))]
[JsonSerializable(typeof(StaticWebAssetsEndpointsManifest))]
internal sealed partial class StaticWebAssetsJsonContext : JsonSerializerContext;
