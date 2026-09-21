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
        [SupportedOSPlatform("browser")]
        public ViuApplicationBuilder ConfigureBrowser(
            Action<BrowserApplicationOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);

            var browserOptions = new BrowserApplicationOptions();
            configure?.Invoke(browserOptions);

            return builder.ConfigureHost(
                new BrowserViuApplicationHostFactory(
                    browserOptions.Hydrate,
                    browserOptions.MountTargetSelector));
        }
    }
}
