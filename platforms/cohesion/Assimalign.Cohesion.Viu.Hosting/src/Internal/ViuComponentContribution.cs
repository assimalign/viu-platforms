using System;

using Assimalign.Viu.Components;

namespace Assimalign.Cohesion.Viu.Hosting;

/// <summary>One ordered component-registration callback applied when the application is built.</summary>
internal sealed class ViuComponentContribution
{
    public ViuComponentContribution(Action<ComponentFactory> register)
    {
        ArgumentNullException.ThrowIfNull(register);
        Register = register;
    }

    public Action<ComponentFactory> Register { get; }
}
