using System;
using System.Runtime.Versioning;

using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Viu;
using Assimalign.Viu.Browser;

namespace Assimalign.Cohesion.Viu.Hosting.Browser;

[SupportedOSPlatform("browser")]
internal sealed class BrowserViuApplicationHostFactory : IViuApplicationHostFactory
{
    public static BrowserViuApplicationHostFactory Instance { get; } = new();

    private BrowserViuApplicationHostFactory()
    {
    }

    public IApplication CreateApplication(ApplicationOptions applicationOptions)
    {
        ArgumentNullException.ThrowIfNull(applicationOptions);

        // ConfigureBrowser registers one options instance in the application's container; the
        // values are copied here, so mutating that instance after Build has no effect.
        BrowserApplicationOptions browserOptions =
            applicationOptions.Services?.GetService<BrowserApplicationOptions>()
            ?? new BrowserApplicationOptions();
        bool hydrate = browserOptions.Hydrate;
        string mountTargetSelector = browserOptions.MountTargetSelector;
        ArgumentException.ThrowIfNullOrEmpty(mountTargetSelector);

        var builder = new BrowserApplicationBuilder();
        builder.ConfigureApplication(options =>
        {
            options.RootComponent = applicationOptions.RootComponent;
            options.Components = applicationOptions.Components;
            options.Services = applicationOptions.Services;
            options.State = applicationOptions.State;
            options.Directives = applicationOptions.Directives;
            options.ErrorHandler = applicationOptions.ErrorHandler;
            options.WarnHandler = applicationOptions.WarnHandler;
            options.EventObserver = applicationOptions.EventObserver;
        });
        builder.ConfigureBrowser(options =>
        {
            options.Hydrate = hydrate;
            options.MountTargetSelector = mountTargetSelector;
        });

        return builder.Build();
    }
}
