using System;
using System.Collections.Generic;

using Assimalign.Viu.Router;

using ViuRouter = Assimalign.Viu.Router.Router;

namespace Assimalign.Cohesion.Viu.Routing;

/// <summary>Configures the application router registered by <c>AddRouting</c>.</summary>
/// <remarks>
/// A service container holds one options instance, and repeated <c>AddRouting</c> calls configure
/// it. The router reads these values once, when it is first resolved. Not thread-safe.
/// </remarks>
public sealed class ViuRoutingOptions
{
    private readonly List<Action<IServiceProvider, ViuRouter>> _routerConfigurations = [];

    /// <summary>
    /// Gets the application's top-level route records. They precede routes contributed through
    /// <c>AddRoutes</c> in the router's route table.
    /// </summary>
    public IList<RouteRecord> Routes { get; } = [];

    /// <summary>Gets or sets the scroll decision applied to each confirmed navigation.</summary>
    public ScrollBehavior? ScrollBehavior { get; set; }

    /// <summary>Gets or sets the default active link class, or null to keep the router default.</summary>
    public string? LinkActiveClass { get; set; }

    /// <summary>Gets or sets the default exact-active link class, or null to keep the router default.</summary>
    public string? LinkExactActiveClass { get; set; }

    /// <summary>Adds a callback that configures the router after construction, such as a guard.</summary>
    /// <param name="configure">The callback, invoked in registration order with the application's services.</param>
    /// <returns>These options.</returns>
    public ViuRoutingOptions ConfigureRouter(Action<IServiceProvider, ViuRouter> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _routerConfigurations.Add(configure);
        return this;
    }

    internal IReadOnlyList<Action<IServiceProvider, ViuRouter>> RouterConfigurations => _routerConfigurations;
}
