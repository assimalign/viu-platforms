using Assimalign.Cohesion.ApplicationModel;

namespace Assimalign.Cohesion.Viu.ApplicationModel;

/// <summary>Configures validation and the standard Web resource planning overrides.</summary>
public sealed class ViuWebResourceOptions : ResourceOptions
{
    /// <summary>Gets or sets whether the manifest must contain valid web.viu properties. Defaults to true.</summary>
    public bool ValidateClient { get; set; } = true;
}
