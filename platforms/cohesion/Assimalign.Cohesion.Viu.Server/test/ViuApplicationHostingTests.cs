using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Assimalign.Cohesion.Configuration;
using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Cohesion.Hosting;
using Assimalign.Cohesion.Http;
using Assimalign.Cohesion.Viu.Hosting;
using Assimalign.Cohesion.Web;
using Assimalign.Cohesion.Web.Hosting;
using Assimalign.Viu.Components;
using Assimalign.Viu.ServerRenderer;

using Shouldly;

using Xunit;

namespace Assimalign.Cohesion.Viu.Server.Tests;

public sealed class ViuApplicationHostingTests
{
    [Fact(DisplayName = "Cohesion Viu Server - host: composition runtime owns a real middleware lifetime")]
    public async Task ConfigureServerRendering_StartAndStop_ShouldRunApplicationMiddleware()
    {
        // Arrange
        List<string> events = [];
        ViuApplicationBuilder builder = ViuApplication.CreateBuilder()
            .ConfigureServerRendering()
            .ConfigureApplication(options => options.RootComponent = new TextNode("server"));
        await using ViuApplication application = builder.Build();
        application.Use(async (context, next) =>
        {
            events.Add("starting");
            await next(context);
            events.Add("stopped");
        });

        // Act
        await application.StartAsync();

        // Assert
        application.ApplicationContext.IsRunning.ShouldBeTrue();
        events.ShouldBe(["starting"]);

        // Act
        await application.StopAsync();

        // Assert
        application.ApplicationContext.IsRunning.ShouldBeFalse();
        events.ShouldBe(["starting", "stopped"]);
    }

    [Fact(DisplayName = "Cohesion Viu Server - web host: starts and stops the borrowed Viu application")]
    public async Task AddViuServerApplication_WebHostLifecycle_ShouldFlowToViuApplication()
    {
        // Arrange
        ViuApplicationBuilder viuBuilder = ViuApplication.CreateBuilder()
            .ConfigureServerRendering()
            .ConfigureApplication(options => options.RootComponent = new TextNode("server"));
        await using ViuApplication viuApplication = viuBuilder.Build();
        WebApplicationBuilder webBuilder = WebApplication.CreateBuilder()
            .AddViuServerApplication(viuApplication);
        await using WebApplication webApplication = webBuilder.Build();

        // Act
        await ((IHost)webApplication).StartAsync();

        // Assert
        viuApplication.ApplicationContext.IsRunning.ShouldBeTrue();

        // Act
        await ((IHost)webApplication).StopAsync();

        // Assert
        viuApplication.ApplicationContext.IsRunning.ShouldBeFalse();
    }

    [Fact(DisplayName = "Cohesion Viu Server - renderer: borrows composition into fresh request applications")]
    public async Task UseViuServerRenderer_HostedApplication_ShouldFlowServicesAndFreezeRegistry()
    {
        // Arrange
        RenderMessage message = new("hosted service");
        ComponentReference reference = ComponentReference.ForName("service-message");
        ComponentFactory components = new();
        components.Register(ComponentRegistration.Define(
            "service-message",
            new ComponentContract(),
            context =>
            {
                RenderMessage service = (RenderMessage)context.Services!
                    .GetService(typeof(RenderMessage))!;
                IConfiguration configuration = (IConfiguration)context.Services
                    .GetService(typeof(IConfiguration))!;
                return _ => new TextNode(
                    $"{service.Value}: {configuration["render:message"]}");
            }));
        ViuApplicationBuilder viuBuilder = ViuApplication.CreateBuilder()
            .ConfigureServerRendering()
            .ConfigureApplication(options =>
            {
                options.RootComponent = new ComponentNode(reference);
                options.Components = components;
        });
        viuBuilder.Services.AddSingleton(message);
        viuBuilder.Configuration["render:message"] = "hosted configuration";
        await using ViuApplication viuApplication = viuBuilder.Build();
        ViuApplicationServerRenderRequestScopeFactory requestScopes = new(viuApplication);
        ServerRenderRequest<IHttpContext> firstRequest = new(
            requestScopes.RootComponent,
            new TestHttpContext("/first", HttpMethod.Get));
        ServerRenderRequest<IHttpContext> secondRequest = new(
            requestScopes.RootComponent,
            new TestHttpContext("/second", HttpMethod.Get));
        await using IServerRenderRequestScope firstScope =
            await requestScopes.CreateAsync(firstRequest);
        await using IServerRenderRequestScope secondScope =
            await requestScopes.CreateAsync(secondRequest);
        ServerRenderRegistry serverRenders = new();
        WebApplicationBuilder webBuilder = WebApplication.CreateBuilder();
        await using WebApplication webApplication = webBuilder.Build();
        TestHttpContext responseContext = new("/", HttpMethod.Get);

        // Act
        webApplication.UseViuServerRenderer(viuApplication, serverRenders);
        IWebApplicationPipeline pipeline =
            ((IWebApplicationPipelineBuilder)webApplication).Build();
        await pipeline.ExecuteAsync(responseContext);

        // Assert
        firstScope.Application.ShouldNotBeSameAs(secondScope.Application);
        firstScope.Application.Context.ShouldNotBeSameAs(secondScope.Application.Context);
        firstScope.RenderContext.ShouldNotBeSameAs(secondScope.RenderContext);
        firstScope.Application.Context.Services.ShouldBeSameAs(
            viuApplication.ApplicationContext.Services);
        responseContext.ReadResponseBody().ShouldBe(
            "hosted service: hosted configuration");
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
            serverRenders.Register(new ServerRenderRegistration(
                ComponentReference.ForName("too-late"),
                static (_, _, _, _) => Task.CompletedTask)));
        exception.Message.ShouldContain("frozen", Case.Insensitive);
    }

    private sealed record RenderMessage(string Value);
}
