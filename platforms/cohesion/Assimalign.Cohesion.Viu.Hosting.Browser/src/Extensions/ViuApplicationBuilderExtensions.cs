using System;
using System.Net.Http;
using System.Runtime.Versioning;

using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Cohesion.Hosting;
using Assimalign.Viu.Browser;

namespace Assimalign.Cohesion.Viu.Hosting.Browser;

/// <summary>Provides browser-host composition for Cohesion Viu applications.</summary>
public static class ViuApplicationBuilderExtensions
{
    extension(ViuApplicationBuilder builder)
    {
        /// <summary>Loads the public resource configuration before the Viu runtime starts.</summary>
        /// <param name="httpClient">The borrowed HTTP client, normally configured with the site's base address.</param>
        /// <param name="configure">Optional document path and timeout settings.</param>
        /// <returns>The same application builder.</returns>
        /// <remarks>Missing or invalid documents retain an empty configuration and invoke the application's warning handler.</remarks>
        public ViuApplicationBuilder AddClientConfiguration(
            HttpClient httpClient,
            Action<ViuClientConfigurationOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(httpClient);
            ViuClientConfigurationOptions options = new();
            configure?.Invoke(options);
            ArgumentException.ThrowIfNullOrWhiteSpace(options.Path);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.Timeout, TimeSpan.Zero);
            string path = options.Path;
            TimeSpan timeout = options.Timeout;
            ViuClientConfigurationProvider provider = new();
            builder.Services.Add(ServiceDescriptor.Singleton<ViuClientConfigurationProvider>(_ => provider));
            builder.Services.Add(ServiceDescriptor.Singleton<IHostService>(_ => new ViuClientConfigurationHostService(
                httpClient, provider, path, timeout, builder.ApplicationOptions.WarnHandler)));
            return builder;
        }

        /// <summary>Selects the Viu browser runtime and configures its mount behavior.</summary>
        /// <param name="configure">
        /// An optional action that configures hydration and the DOM mount selector.
        /// </param>
        /// <returns>The host-neutral application builder.</returns>
        /// <remarks>
        /// The browser options are one singleton instance in <see cref="ViuApplicationBuilder.Services"/>.
        /// Repeated calls configure that same instance, so a later call changes only the values it
        /// sets. The runtime reads the options when the application is built.
        /// </remarks>
        [SupportedOSPlatform("browser")]
        public ViuApplicationBuilder ConfigureBrowser(
            Action<BrowserApplicationOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);

            BrowserApplicationOptions browserOptions = GetOrAddBrowserOptions(builder.Services);
            configure?.Invoke(browserOptions);
            ArgumentException.ThrowIfNullOrEmpty(browserOptions.MountTargetSelector);

            return builder.ConfigureHost(BrowserViuApplicationHostFactory.Instance);
        }
    }

    private static BrowserApplicationOptions GetOrAddBrowserOptions(IServiceProviderBuilder services)
    {
        IServiceContainer container = services.Container;
        for (int index = container.Count - 1; index >= 0; index--)
        {
            ServiceDescriptor descriptor = container[index];
            if (descriptor.ServiceType != typeof(BrowserApplicationOptions))
            {
                continue;
            }

            // The last registration is the one the runtime resolves; configure it only when it
            // is an instance this call can mutate.
            if (descriptor.ImplementationInstance is BrowserApplicationOptions registeredOptions)
            {
                return registeredOptions;
            }

            break;
        }

        BrowserApplicationOptions browserOptions = new();
        services.Add(ServiceDescriptor.Singleton(browserOptions));
        return browserOptions;
    }
}
