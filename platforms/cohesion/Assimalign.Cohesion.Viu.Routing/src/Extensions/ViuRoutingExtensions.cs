using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Cohesion.Viu.Hosting;
using Assimalign.Viu;
using Assimalign.Viu.Browser.Router;
using Assimalign.Viu.Router;

using ViuRouter = Assimalign.Viu.Router.Router;

namespace Assimalign.Cohesion.Viu.Routing;

/// <summary>
/// Registers Viu routing in a Cohesion-hosted application and installs it around the application
/// lifetime.
/// </summary>
/// <remarks>
/// <c>AddRouting</c> (build time) registers one router, its history, and the router components.
/// <c>UseRouter</c> (after build) resolves that same router and installs its lifetime middleware.
/// Every registration is an explicit instance or factory; no type is activated by reflection.
/// </remarks>
public static class ViuRoutingExtensions
{
    /// <param name="services">The application's service-provider builder.</param>
    extension(IServiceProviderBuilder services)
    {
        /// <summary>Registers the application router, its default history, and the router components.</summary>
        /// <param name="configure">An optional action that adds routes and configures the router.</param>
        /// <returns>The same service-provider builder.</returns>
        /// <remarks>
        /// Repeated calls configure the same <see cref="ViuRoutingOptions"/>. The router and its
        /// history are singletons created on first resolution and disposed with the provider. The
        /// default history is browser web history in a browser and memory history elsewhere;
        /// register an <see cref="IRouterHistory"/> to replace it. <c>RouterView</c> and
        /// <c>RouterLink</c> are contributed under their type and template names unless the
        /// application already registers them.
        /// </remarks>
        [OverloadResolutionPriority(1)]
        public IServiceProviderBuilder AddRouting(Action<ViuRoutingOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            ViuRoutingOptions options = GetRegisteredOptions(services) ?? AddRoutingServices(services);
            configure?.Invoke(options);
            return services;
        }

        /// <summary>Configures routing using services from the application's built provider.</summary>
        /// <param name="configure">The callback receiving the provider and shared routing options.</param>
        /// <returns>The same service-provider builder.</returns>
        /// <remarks>
        /// The callback runs once when the singleton router is first resolved, after registration-time
        /// configuration and before route contributions, router construction, and ConfigureRouter callbacks.
        /// Provider-aware callbacks run in registration order and can resolve services registered later.
        /// The provider is the application's root provider; callbacks must not resolve the router being
        /// constructed. Existing history selection, component registration, and disposal behavior apply.
        /// </remarks>
        public IServiceProviderBuilder AddRouting(Action<IServiceProvider, ViuRoutingOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            services.AddRouting();
            return services.Add(ServiceDescriptor.Singleton(new ViuRoutingConfiguration(configure)));
        }

        /// <summary>Contributes route records created from the application's services.</summary>
        /// <param name="routes">The factory invoked once, when the router is first resolved.</param>
        /// <returns>The same service-provider builder.</returns>
        /// <remarks>
        /// Contributed routes follow <see cref="ViuRoutingOptions.Routes"/> in the route table, in
        /// registration order. Contributions take effect only when routing is registered with
        /// <c>AddRouting</c>.
        /// </remarks>
        public IServiceProviderBuilder AddRoutes(Func<IServiceProvider, IEnumerable<RouteRecord>> routes)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(routes);

            return services.Add(ServiceDescriptor.Singleton(new ViuRouteContribution(routes)));
        }
    }

    /// <param name="application">The built Cohesion-hosted Viu application.</param>
    extension(ViuApplication application)
    {
        /// <summary>Installs the registered router around the application lifetime.</summary>
        /// <returns>The same application.</returns>
        /// <exception cref="InvalidOperationException">
        /// Routing was not registered with <c>AddRouting</c>, or the application has already started.
        /// </exception>
        /// <remarks>
        /// The router's initial navigation settles before the root mounts, and the router-to-DOM
        /// bridge is removed when the application stops or fails.
        /// </remarks>
        public ViuApplication UseRouter()
        {
            ArgumentNullException.ThrowIfNull(application);

            ViuRouter router = application.Context.Services.GetService<ViuRouter>()
                ?? throw new InvalidOperationException(
                    "Routing has not been registered. Call builder.Services.AddRouting() before UseRouter().");
            ((IApplication)application).UseRouter(router);
            return application;
        }
    }

    private static ViuRoutingOptions? GetRegisteredOptions(IServiceProviderBuilder services)
    {
        foreach (ServiceDescriptor descriptor in services.Container)
        {
            if (descriptor.ServiceType == typeof(ViuRoutingOptions)
                && descriptor.ImplementationInstance is ViuRoutingOptions options)
            {
                return options;
            }
        }

        return null;
    }

    private static ViuRoutingOptions AddRoutingServices(IServiceProviderBuilder services)
    {
        ViuRoutingOptions options = new();
        services.Add(ServiceDescriptor.Singleton(options));
        services.TryAddSingleton<IRouterHistory>(static _ => CreateDefaultHistory());
        services.Add(ServiceDescriptor.Singleton<ViuRouter>(provider => CreateRouter(provider, options)));
        services.AddComponents(RouterComponentRegistrations.Register);
        return options;
    }

    private static IRouterHistory CreateDefaultHistory() =>
        OperatingSystem.IsBrowser()
            ? BrowserRouterHistory.CreateWeb()
            : RouterHistory.CreateMemory();

    private static ViuRouter CreateRouter(IServiceProvider services, ViuRoutingOptions options)
    {
        foreach (ViuRoutingConfiguration configuration in services.GetServices<ViuRoutingConfiguration>())
        {
            configuration.Configure(services, options);
        }

        List<RouteRecord> routes = [.. options.Routes];
        foreach (ViuRouteContribution contribution in services.GetServices<ViuRouteContribution>())
        {
            routes.AddRange(contribution.CreateRoutes(services));
        }

        ViuRouter router = new(services.GetRequiredService<IRouterHistory>(), routes);
        try
        {
            if (options.ScrollBehavior is { } scrollBehavior)
            {
                router.ScrollBehavior = scrollBehavior;
            }

            if (options.LinkActiveClass is { } linkActiveClass)
            {
                router.LinkActiveClass = linkActiveClass;
            }

            if (options.LinkExactActiveClass is { } linkExactActiveClass)
            {
                router.LinkExactActiveClass = linkExactActiveClass;
            }

            foreach (Action<IServiceProvider, ViuRouter> configure in options.RouterConfigurations)
            {
                configure(services, router);
            }

            return router;
        }
        catch
        {
            // The provider never receives a router whose configuration failed, so it cannot
            // dispose it; release the history listener here.
            router.Dispose();
            throw;
        }
    }
}
