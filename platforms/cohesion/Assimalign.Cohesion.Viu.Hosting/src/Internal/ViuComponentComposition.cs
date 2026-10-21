using System;
using System.Collections.Generic;

using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Viu.Components;

namespace Assimalign.Cohesion.Viu.Hosting;

/// <summary>Composes the application's component factory from options and service contributions.</summary>
/// <remarks>
/// Contributions join the builder-owned default factory, so resolution remains a single lookup.
/// When the application assigned its own factory, that factory keeps precedence and contributions
/// are consulted only after it misses.
/// </remarks>
internal sealed class ViuComponentComposition
{
    public ComponentFactory DefaultComponents { get; set; } = new();

    public IComponentFactory? ExplicitComponents { get; set; }

    public static ViuComponentComposition GetOrAdd(IServiceProviderBuilder services)
    {
        foreach (ServiceDescriptor descriptor in services.Container)
        {
            if (descriptor.ImplementationInstance is ViuComponentComposition composition)
            {
                return composition;
            }
        }

        ViuComponentComposition added = new();
        services.AddSingleton(added);
        services.TryAddSingleton<IComponentFactory>(provider => Compose(
            added.ExplicitComponents,
            added.DefaultComponents,
            provider.GetServices<ViuComponentContribution>()));
        return added;
    }

    public static IComponentFactory Compose(
        IComponentFactory? explicitComponents,
        ComponentFactory defaultComponents,
        IEnumerable<ViuComponentContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(defaultComponents);
        ArgumentNullException.ThrowIfNull(contributions);

        if (explicitComponents is null || ReferenceEquals(explicitComponents, defaultComponents))
        {
            if (!Apply(defaultComponents, contributions))
            {
                defaultComponents.RegisterGeneratedComponents();
            }
            return defaultComponents;
        }

        ComponentFactory contributedComponents = new();
        return Apply(contributedComponents, contributions)
            ? new LayeredComponentFactory(explicitComponents, contributedComponents)
            : explicitComponents;
    }

    private static bool Apply(
        ComponentFactory components,
        IEnumerable<ViuComponentContribution> contributions)
    {
        bool isApplied = false;
        foreach (ViuComponentContribution contribution in contributions)
        {
            isApplied = true;
            try
            {
                contribution.Register(components);
            }
            catch (ArgumentException exception) when (exception.GetType() == typeof(ArgumentException))
            {
                // ComponentFactory.Register reports a duplicate reference as a plain
                // ArgumentException that names the conflicting key.
                throw new InvalidOperationException(
                    "A Viu component contribution registered a component reference that is " +
                    $"already registered. {exception.Message}",
                    exception);
            }
        }

        return isApplied;
    }
}
