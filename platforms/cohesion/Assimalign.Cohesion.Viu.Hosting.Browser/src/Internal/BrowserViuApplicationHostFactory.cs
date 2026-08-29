using System;
using System.Runtime.Versioning;

using Assimalign.Viu;
using Assimalign.Viu.Browser;

namespace Assimalign.Cohesion.Viu.Hosting.Browser;

[SupportedOSPlatform("browser")]
internal sealed class BrowserViuApplicationHostFactory : IViuApplicationHostFactory
{
    private readonly bool _hydrate;
    private readonly string _mountTargetSelector;

    public BrowserViuApplicationHostFactory(
        bool hydrate,
        string mountTargetSelector)
    {
        ArgumentException.ThrowIfNullOrEmpty(mountTargetSelector);
        _hydrate = hydrate;
        _mountTargetSelector = mountTargetSelector;
    }

    public IApplication CreateApplication(ApplicationOptions applicationOptions)
    {
        ArgumentNullException.ThrowIfNull(applicationOptions);

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
            options.Hydrate = _hydrate;
            options.MountTargetSelector = _mountTargetSelector;
        });

        return builder.Build();
    }
}
