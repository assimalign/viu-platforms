using System.Text.Json.Serialization;

namespace Assimalign.Cohesion.Viu.Hosting;

/// <summary>Provides trimming-safe JSON metadata for the browser client document.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ViuClientConfiguration))]
public partial class ViuClientConfigurationJsonContext : JsonSerializerContext
{
}
