using System.Text.Json.Serialization;

namespace Assimalign.Cohesion.Viu.Server;

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNameCaseInsensitive = false)]
[JsonSerializable(typeof(StaticWebAssetsRuntimeManifest))]
[JsonSerializable(typeof(StaticWebAssetsEndpointsManifest))]
[JsonSerializable(typeof(ViuClientDocument))]
internal sealed partial class StaticWebAssetsJsonContext : JsonSerializerContext;
