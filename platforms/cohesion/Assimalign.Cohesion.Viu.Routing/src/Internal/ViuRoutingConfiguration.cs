using System;

namespace Assimalign.Cohesion.Viu.Routing;

/// <summary>One ordered routing-options callback applied when the singleton router is created.</summary>
internal sealed class ViuRoutingConfiguration
{
    public ViuRoutingConfiguration(Action<IServiceProvider, ViuRoutingOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Configure = configure;
    }

    public Action<IServiceProvider, ViuRoutingOptions> Configure { get; }
}
