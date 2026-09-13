using System;

using Assimalign.Viu.Components;
using Assimalign.Viu.Router;

namespace Assimalign.Cohesion.Viu.Markdown;

/// <summary>Registers Markdown components and their router dependencies without type discovery.</summary>
public static class MarkdownComponents
{
    /// <summary>Adds typed and template-name registrations, preserving registrations already supplied by the caller.</summary>
    /// <param name="components">The application factory mutated during composition; repeat calls are idempotent.</param>
    public static void Register(ComponentFactory components)
    {
        ArgumentNullException.ThrowIfNull(components);
        Register(components, MarkdownPageView.Registration, nameof(MarkdownPageView));
        Register(components, MarkdownNavigation.Registration, nameof(MarkdownNavigation));
        Register(components, RouterLink.Registration, nameof(RouterLink));
        Register(components, RouterView.Registration, nameof(RouterView));
    }

    private static void Register(ComponentFactory components, ComponentRegistration registration, string name)
    {
        if (!components.TryResolve(registration.Reference, out _))
        {
            components.Register(registration);
        }
        ComponentReference namedReference = ComponentReference.ForName(name);
        if (!components.TryResolve(namedReference, out _))
        {
            components.Register(new ComponentRegistration(namedReference, registration.Contract, registration.Activator));
        }
    }
}
