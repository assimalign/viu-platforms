using System;
using System.Collections.Generic;

using Assimalign.Viu.Router;

namespace Assimalign.Cohesion.Viu.Routing;

/// <summary>One ordered route-record factory resolved when the router is first created.</summary>
internal sealed class ViuRouteContribution
{
    private readonly Func<IServiceProvider, IEnumerable<RouteRecord>> _routes;

    public ViuRouteContribution(Func<IServiceProvider, IEnumerable<RouteRecord>> routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        _routes = routes;
    }

    public IEnumerable<RouteRecord> CreateRoutes(IServiceProvider services) =>
        _routes(services) ?? throw new InvalidOperationException(
            "A Viu route contribution returned null instead of route records.");
}
