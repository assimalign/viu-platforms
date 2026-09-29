using System;

using Assimalign.Viu.Components;
using Assimalign.Viu.Router;

namespace Assimalign.Cohesion.Viu.Routing;

/// <summary>Registers the router components under their type and template-name references.</summary>
internal static class RouterComponentRegistrations
{
    public static void Register(ComponentFactory components)
    {
        ArgumentNullException.ThrowIfNull(components);
        Register(components, RouterView.Registration, nameof(RouterView));
        Register(components, RouterLink.Registration, nameof(RouterLink));
    }

    private static void Register(
        ComponentFactory components,
        ComponentRegistration registration,
        string name)
    {
        // The router publishes type references while compiled templates reference static tags by
        // name, so both identities are registered. An existing registration is kept, which lets
        // an application or another contribution supply either identity first.
        if (!components.TryResolve(registration.Reference, out _))
        {
            components.Register(registration);
        }

        ComponentReference namedReference = ComponentReference.ForName(name);
        if (!components.TryResolve(namedReference, out _))
        {
            components.Register(new ComponentRegistration(
                namedReference,
                registration.Contract,
                registration.Activator));
        }
    }
}
