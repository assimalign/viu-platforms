using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Cohesion.Viu.Hosting;
using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Router;

using Shouldly;

using Xunit;

using ViuRouter = Assimalign.Viu.Router.Router;

namespace Assimalign.Cohesion.Viu.Routing.Tests;

public sealed class ViuRoutingRegistrationTests
{
    [Fact]
    public async Task ExplicitCatalogCallbacksStillWorkWithRoutingAndPublishedGeneratedCatalogs()
    {
        GeneratedComponentCatalogs.Register("Cohesion.Routing.Tests",
            static components => components.Register(HomePage.Registration));
        ViuApplicationBuilder builder = CreateBuilder(new RecordingHostFactory());
        builder.Services.AddComponents(static components => components.Register(HomePage.Registration));
        builder.Services.AddRouting(routing => routing.Routes.Add<HomePage>("/", "Home"));

        await using ViuApplication application = builder.Build();

        application.Context.Services.GetRequiredService<IComponentFactory>()
            .Resolve(HomePage.Registration.Reference).ShouldBeSameAs(HomePage.Registration);
    }

    [Fact]
    public async Task RouteCollectionConveniencesWorkThroughTheOptionsList()
    {
        ViuApplicationBuilder builder = CreateBuilder(new RecordingHostFactory());
        builder.Services.AddRouting(routing =>
        {
            routing.Routes.Add("/", "HomePage", "Home");
            routing.Routes.Add<HomePage>("/typed", "Typed home");
        });
        await using ViuApplication application = builder.Build();
        ViuRouter router = application.Context.Services.GetRequiredService<ViuRouter>();

        router.Resolve("/").IsMatched.ShouldBeTrue();
        router.Resolve("/").Meta["title"].ShouldBe("Home");
        router.Resolve("/typed").IsMatched.ShouldBeTrue();
        router.Resolve("/typed").Meta["title"].ShouldBe("Typed home");
        ViuRoutingOptions options = application.Context.Services.GetRequiredService<ViuRoutingOptions>();
        options.Routes[1].Component.ShouldBeOfType<ComponentNode>().Component
            .ShouldBe(HomePage.Registration.Reference);
    }

    private sealed class HomePage : IComponentDefinition
    {
        public static ComponentRegistration Registration { get; } = ComponentRegistration.Define(
            "RegisteredHomePage", new ComponentContract(), static _ => static _ => new TextNode("home"));
    }

    [Fact]
    public async Task AddRoutingRegistersOneRouterWithHistoryRoutesAndComponents()
    {
        RouteRecord home = new("/", component: new TextNode("home"));
        var factory = new RecordingHostFactory();
        ViuApplicationBuilder builder = CreateBuilder(factory);

        builder.Services.AddRouting(routing => routing.Routes.Add(home));

        await using ViuApplication application = builder.Build();

        IServiceProvider services = application.Context.Services;
        ViuRouter router = services.GetRequiredService<ViuRouter>();
        router.ShouldBeSameAs(services.GetRequiredService<ViuRouter>());
        router.Resolve("/").IsMatched.ShouldBeTrue();
        services.GetRequiredService<IRouterHistory>().ShouldNotBeNull();

        IComponentFactory components = factory.Options!.Components;
        components.Resolve(RouterView.Registration.Reference).ShouldBeSameAs(RouterView.Registration);
        components.Resolve(RouterLink.Registration.Reference).ShouldBeSameAs(RouterLink.Registration);
        components.Resolve(ComponentReference.ForName(nameof(RouterView))).Contract
            .ShouldBeSameAs(RouterView.Registration.Contract);
        components.Resolve(ComponentReference.ForName(nameof(RouterLink))).Contract
            .ShouldBeSameAs(RouterLink.Registration.Contract);
    }

    [Fact]
    public async Task RepeatedAddRoutingConfiguresOneOptionsInstanceAndRouter()
    {
        ViuApplicationBuilder builder = CreateBuilder(new RecordingHostFactory());

        builder.Services
            .AddRouting(routing => routing.Routes.Add(new RouteRecord("/", component: new TextNode("home"))))
            .AddRouting(routing => routing.Routes.Add(new RouteRecord("/about", component: new TextNode("about"))))
            .AddRouting();

        builder.Services.Container.Count(descriptor => descriptor.ServiceType == typeof(ViuRouter))
            .ShouldBe(1);
        builder.Services.Container.Count(descriptor => descriptor.ServiceType == typeof(ViuRoutingOptions))
            .ShouldBe(1);

        await using ViuApplication application = builder.Build();

        ViuRouter router = application.Context.Services.GetRequiredService<ViuRouter>();
        router.Resolve("/").IsMatched.ShouldBeTrue();
        router.Resolve("/about").IsMatched.ShouldBeTrue();
    }

    [Fact]
    public async Task ContributedRoutesJoinTheRouterAndReceiveTheProvider()
    {
        var marker = new RouteMarker();
        ViuApplicationBuilder builder = CreateBuilder(new RecordingHostFactory());
        builder.Services.AddSingleton(marker);

        // Contributions may be registered before routing itself.
        builder.Services.AddRoutes(services =>
        {
            services.GetRequiredService<RouteMarker>().ShouldBeSameAs(marker);
            return [new RouteRecord("/docs", component: new TextNode("docs"))];
        });
        builder.Services.AddRouting(routing =>
            routing.Routes.Add(new RouteRecord("/", component: new TextNode("home"))));

        await using ViuApplication application = builder.Build();

        ViuRouter router = application.Context.Services.GetRequiredService<ViuRouter>();
        router.Resolve("/").IsMatched.ShouldBeTrue();
        router.Resolve("/docs").IsMatched.ShouldBeTrue();
        router.Resolve("/missing").IsMatched.ShouldBeFalse();
    }

    [Fact]
    public async Task ApplicationRegisteredHistoryReplacesTheDefaultInEitherOrder()
    {
        using IRouterHistory before = RouterHistory.CreateMemory();
        ViuApplicationBuilder earlyBuilder = CreateBuilder(new RecordingHostFactory());
        earlyBuilder.Services.AddSingleton<IRouterHistory>(before);
        earlyBuilder.Services.AddRouting();

        await using (ViuApplication application = earlyBuilder.Build())
        {
            application.Context.Services.GetRequiredService<IRouterHistory>().ShouldBeSameAs(before);
            earlyBuilder.Services.Container
                .Count(descriptor => descriptor.ServiceType == typeof(IRouterHistory))
                .ShouldBe(1);
        }

        using IRouterHistory after = RouterHistory.CreateMemory();
        ViuApplicationBuilder lateBuilder = CreateBuilder(new RecordingHostFactory());
        lateBuilder.Services.AddRouting();
        lateBuilder.Services.AddSingleton<IRouterHistory>(after);

        await using ViuApplication lateApplication = lateBuilder.Build();

        lateApplication.Context.Services.GetRequiredService<IRouterHistory>().ShouldBeSameAs(after);
    }

    [Fact]
    public async Task OptionsConfigureTheRouterAndCallbacksRunInRegistrationOrder()
    {
        List<string> calls = [];
        ScrollBehavior scrollBehavior = static (_, _, _) => Task.FromResult<ScrollTarget?>(null);
        ViuApplicationBuilder builder = CreateBuilder(new RecordingHostFactory());

        builder.Services.AddRouting(routing =>
        {
            routing.ScrollBehavior = scrollBehavior;
            routing.LinkActiveClass = "active";
            routing.LinkExactActiveClass = "exact-active";
            routing
                .ConfigureRouter((services, router) =>
                {
                    services.GetRequiredService<ViuRoutingOptions>().ShouldBeSameAs(routing);
                    calls.Add("first");
                })
                .ConfigureRouter((_, _) => calls.Add("second"));
        });

        await using ViuApplication application = builder.Build();

        ViuRouter router = application.Context.Services.GetRequiredService<ViuRouter>();
        router.ScrollBehavior.ShouldBeSameAs(scrollBehavior);
        router.LinkActiveClass.ShouldBe("active");
        router.LinkExactActiveClass.ShouldBe("exact-active");
        calls.ShouldBe(["first", "second"]);
    }

    [Fact]
    public async Task UseRouterRequiresAddRouting()
    {
        await using ViuApplication application = CreateBuilder(new RecordingHostFactory()).Build();

        Should.Throw<InvalidOperationException>(() => application.UseRouter())
            .Message.ShouldContain("AddRouting");
    }

    [Fact]
    public async Task UseRouterSettlesInitialNavigationBeforeTheRuntimeRuns()
    {
        var factory = new RecordingHostFactory();
        ViuApplicationBuilder builder = CreateBuilder(factory);
        builder.Services.AddRouting(routing =>
            routing.Routes.Add(new RouteRecord("/", component: new TextNode("home"))));

        await using ViuApplication application = builder.Build();

        application.UseRouter().ShouldBeSameAs(application);

        ApplicationMiddleware middleware = factory.Application!.Middleware.ShouldHaveSingleItem();
        ViuRouter router = application.Context.Services.GetRequiredService<ViuRouter>();
        var context = new ApplicationContext(new ApplicationOptions
        {
            RootComponent = new TextNode("root"),
        });
        using var lifetime = new ApplicationLifetime(context);
        lifetime.StartExecution();
        bool isRouteSettledBeforeRuntime = false;

        await middleware(context, _ =>
        {
            isRouteSettledBeforeRuntime = router.CurrentRoute.Value.IsMatched;
            lifetime.SignalRunning();
            return ValueTask.CompletedTask;
        });

        isRouteSettledBeforeRuntime.ShouldBeTrue();
    }

    private static ViuApplicationBuilder CreateBuilder(IViuApplicationHostFactory factory)
    {
        ViuApplicationBuilder builder = ViuApplication.CreateBuilder();
        builder.ConfigureApplication(options => options.RootComponent = new TextNode("root"));
        builder.ConfigureHost(factory);
        return builder;
    }

    private sealed class RouteMarker
    {
    }

    private sealed class RecordingHostFactory : IViuApplicationHostFactory
    {
        public ApplicationOptions? Options { get; private set; }

        public RecordingApplication? Application { get; private set; }

        public IApplication CreateApplication(ApplicationOptions applicationOptions)
        {
            Options = applicationOptions;
            Application = new RecordingApplication(applicationOptions);
            return Application;
        }
    }

    private sealed class RecordingApplication : IApplication
    {
        private readonly ApplicationLifetime _lifetime;

        public RecordingApplication(ApplicationOptions options)
        {
            var context = new ApplicationContext(options);
            _lifetime = new ApplicationLifetime(context);
            Context = context;
        }

        public IApplicationContext Context { get; }

        public List<ApplicationMiddleware> Middleware { get; } = [];

        public IApplication Use(ApplicationMiddleware middleware)
        {
            Middleware.Add(middleware);
            return this;
        }

        public ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            _lifetime.StartExecution();
            _lifetime.SignalRunning();
            return ValueTask.CompletedTask;
        }

        public ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            _lifetime.CompleteStopping();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _lifetime.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
