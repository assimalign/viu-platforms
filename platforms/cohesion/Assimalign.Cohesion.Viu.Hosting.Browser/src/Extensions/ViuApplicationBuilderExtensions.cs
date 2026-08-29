using System;
using System.Runtime.Versioning;

using Assimalign.Viu.Browser;

namespace Assimalign.Cohesion.Viu.Hosting.Browser;

/// <summary>Provides browser-host composition for Cohesion Viu applications.</summary>
public static class ViuApplicationBuilderExtensions
{
    extension(ViuApplicationBuilder builder)
    {
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
