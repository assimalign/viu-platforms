using System;

using Assimalign.Viu;

namespace Assimalign.Cohesion.Viu.Hosting;

internal static class ApplicationOptionsSnapshot
{
    public static ApplicationOptions Create(
        ApplicationOptions source,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new ApplicationOptions
        {
            RootComponent = source.RootComponent,
            Components = source.Components,
            Services = services ?? source.Services,
            State = source.State,
            Directives = source.Directives,
            ErrorHandler = source.ErrorHandler,
            WarnHandler = source.WarnHandler,
            EventObserver = source.EventObserver,
        };
    }
}
