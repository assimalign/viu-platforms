using Assimalign.Cohesion.ApplicationModel;

namespace Assimalign.Cohesion.ApplicationModel;

/// <summary>A typed Viu site descriptor that delegates planning and commands to Web.</summary>
public interface IViuWebResourceDescriptor : IResourceCommandDescriptor
{
    /// <summary>Gets the wrapped Web descriptor.</summary>
    IWebResourceDescriptor Web { get; }

    /// <summary>Gets the browser client assembly name, or an empty string when validation was disabled and it is absent.</summary>
    string ClientAssembly { get; }

    /// <summary>Gets whether the manifest declares an SPA fallback.</summary>
    bool SpaFallback { get; }

    /// <summary>Gets the declared site base path, or an empty string when validation was disabled and it is absent.</summary>
    string BasePath { get; }

    /// <summary>Declares a dependency and retains the Viu descriptor.</summary>
    /// <param name="resource">The prerequisite resource.</param>
    /// <returns>This descriptor.</returns>
    new IViuWebResourceDescriptor DependsOn(IApplicationResourceDescriptor resource);

    /// <summary>Declares dependencies and retains the Viu descriptor.</summary>
    /// <param name="resources">The prerequisite resources.</param>
    /// <returns>This descriptor.</returns>
    new IViuWebResourceDescriptor DependsOn(params IApplicationResourceDescriptor[] resources);
}
