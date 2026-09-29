using System;

using Assimalign.Viu.Components;

namespace Assimalign.Cohesion.Viu.Hosting;

/// <summary>Resolves from an application-assigned factory before the contributed components.</summary>
internal sealed class LayeredComponentFactory : IComponentFactory
{
    private readonly IComponentFactory _primaryComponents;
    private readonly IComponentFactory _contributedComponents;

    public LayeredComponentFactory(
        IComponentFactory primaryComponents,
        IComponentFactory contributedComponents)
    {
        ArgumentNullException.ThrowIfNull(primaryComponents);
        ArgumentNullException.ThrowIfNull(contributedComponents);
        _primaryComponents = primaryComponents;
        _contributedComponents = contributedComponents;
    }

    public ComponentRegistration Resolve(ComponentReference reference)
    {
        return TryResolve(reference, out ComponentRegistration? registration)
            ? registration!
            : throw new InvalidOperationException(
                $"The component reference '{reference}' is not registered; runtime constructor " +
                "discovery is not supported.");
    }

    public bool TryResolve(ComponentReference reference, out ComponentRegistration? registration)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return _primaryComponents.TryResolve(reference, out registration)
            || _contributedComponents.TryResolve(reference, out registration);
    }
}
