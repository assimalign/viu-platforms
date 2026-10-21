using System;

using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Cohesion.Hosting;
using Assimalign.Cohesion.Http;
using Assimalign.Cohesion.Viu.Hosting;

namespace Assimalign.Cohesion.Viu.Server;

/// <summary>Registers Viu rendering and browser assets in the Web host's single container.</summary>
public static class ViuServerServiceProviderBuilderExtensions
{
    extension(IServiceProviderBuilder services)
    {
        /// <summary>Registers host-owned server composition and one DI scope per rendered request.</summary>
        /// <param name="configure">Configures the root, optional catalogs, shell, and diagnostics.</param>
        /// <returns>The same service-provider builder.</returns>
        /// <remarks>Repeated calls configure one composition. Explicit catalogs replace generated defaults.</remarks>
        public IServiceProviderBuilder AddViuServerRendering(Action<ViuServerRenderingOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            foreach (ServiceDescriptor descriptor in services.Container)
            {
                if (descriptor.ImplementationInstance is ViuServerRenderingOptions existing)
                {
                    configure(existing);
                    return services;
                }
            }

            ViuServerRenderingOptions options = new();
            configure(options);
            services.AddSingleton(options);
            services.AddComponents();
            services.AddSingleton<ViuServerRendering>(provider => new ViuServerRendering(provider, options));
            services.AddSingleton<IHostService>(static provider => provider.GetRequiredService<ViuServerRendering>());
            return services;
        }

        /// <summary>Registers browser assets and fallback in the Web host.</summary>
        /// <param name="configure">
        /// Optional explicit asset configuration. When omitted, selects the sole marked client
        /// from viu-client.json or the published wwwroot. Call UseClientOutput() in a callback
        /// when customizing an inferred client.
        /// </param>
        /// <returns>The same service-provider builder.</returns>
        public IServiceProviderBuilder AddViuClient(Action<ViuApplicationOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ViuApplicationOptions options = new();
            if (configure is null)
            {
                options.UseClientOutput();
            }
            else
            {
                configure(options);
            }

            ViuApplicationConfiguration configuration = ViuApplicationConfiguration.Create(options);
            services.AddSingleton<IHttpFeature>(provider => new ViuApplicationFeature(configuration with
            {
                ClientConfiguration = configuration.ClientConfiguration is { Resource: not null } captured
                    ? captured
                    : new ViuClientConfiguration(environment:
                        provider.GetService<IHostEnvironment>()?.Name ?? AppEnvironment.GetEnvironmentName()),
            }));
            return services;
        }
    }
}
