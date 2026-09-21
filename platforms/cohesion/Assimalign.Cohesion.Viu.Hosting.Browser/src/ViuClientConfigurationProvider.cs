namespace Assimalign.Cohesion.Viu.Hosting.Browser;

/// <summary>Provides the immutable document loaded before Viu components start.</summary>
public sealed class ViuClientConfigurationProvider
{
    internal ViuClientConfigurationProvider() { }

    /// <summary>Gets the loaded document, or <see cref="ViuClientConfiguration.Empty"/> if loading failed.</summary>
    public ViuClientConfiguration Current { get; internal set; } = ViuClientConfiguration.Empty;
}
