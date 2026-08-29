using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;

using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Cohesion.Hosting;
using Assimalign.Viu;
using Assimalign.Viu.Components;

using Shouldly;

using Xunit;

namespace Assimalign.Cohesion.Viu.Hosting.Browser.Tests;

public sealed class BrowserHostingTests
{
    [Fact]
    [SupportedOSPlatform("browser")]
    public async Task ConfigureBrowserBuildsTheBrowserRuntimeWithoutStartingJavaScript()
    {
        var root = new TextNode("browser root");
        var service = new BrowserApplicationService();
        ViuApplicationBuilder builder = ViuApplication.CreateBuilder();
        bool configureWasCalled = false;

        builder.Services.Add(ServiceDescriptor.Singleton(service));
        builder.ConfigureApplication(options => options.RootComponent = root);
        ViuApplicationBuilder returnedBuilder = builder.ConfigureBrowser(options =>
        {
            configureWasCalled = true;
            options.Hydrate = true;
            options.MountTargetSelector = "#browser-app";
        });

        ViuApplication application = builder.Build();

        configureWasCalled.ShouldBeTrue();
        returnedBuilder.ShouldBeSameAs(builder);
        application.Context.State.ShouldBe(HostState.Idle);
        application.ApplicationContext.RootComponent.ShouldBeSameAs(root);
        application.ApplicationContext.Services.ShouldBeSameAs(application.Context.Services);
        application.ApplicationContext.Services!
            .GetRequiredService<BrowserApplicationService>()
            .ShouldBeSameAs(service);

        await ((IAsyncDisposable)application).DisposeAsync();
    }

    [Fact]
    [SupportedOSPlatform("browser")]
    public void ConfigureBrowserRejectsAnEmptyMountSelectorBeforeBuild()
    {
        ViuApplicationBuilder builder = ViuApplication.CreateBuilder();

        Should.Throw<ArgumentException>(() => builder.ConfigureBrowser(options =>
            options.MountTargetSelector = string.Empty));
    }

    private sealed class BrowserApplicationService
    {
    }
}
