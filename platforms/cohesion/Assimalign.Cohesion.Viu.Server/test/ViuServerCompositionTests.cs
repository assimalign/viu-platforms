using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Cohesion.Hosting;
using Assimalign.Cohesion.Http;
using Assimalign.Cohesion.Web;
using Assimalign.Cohesion.Web.Hosting;
using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.ServerRenderer;
using Assimalign.Viu.State;

using Shouldly;
using Xunit;

namespace Assimalign.Cohesion.Viu.Server.Tests;

public sealed class ViuServerCompositionTests
{
    static ViuServerCompositionTests()
    {
        GeneratedComponentCatalogs.Register("Cohesion.Server.Tests",
            static components => components.Register(Report.Registration));
        GeneratedServerRenderCatalogs.Register("Cohesion.Server.Tests", static renders => renders.Register(
            new ServerRenderRegistration(Report.Registration.Reference, static (state, _, _, _) =>
            {
                state.Push("generated report");
                return Task.CompletedTask;
            })));
    }

    [Fact]
    public async Task WebHostOwnsCompositionAndUsesGeneratedCatalogsAndDocumentShell()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        int shells = 0;
        ViuServerRenderingOptions? mutableOptions = null;
        builder.Services.AddViuServerRendering(rendering =>
        {
            mutableOptions = rendering;
            rendering.UseRootComponent<Report>();
            rendering.DocumentShell = _ => { Interlocked.Increment(ref shells); return new DocumentShell(); };
        });
        await using WebApplication application = builder.Build();
        ViuServerRendering rendering = application.Context.ServiceProvider.GetRequiredService<ViuServerRendering>();
        rendering.ApplicationContext.Services!.GetRequiredService<IServiceScopeFactory>().ShouldBeSameAs(
            application.Context.ServiceProvider.GetRequiredService<IServiceScopeFactory>());
        mutableOptions!.RootComponent = new TextNode("changed after build");
        application.UseViuServerRenderer(context => context.Request.Path.Value == "/report");
        IWebApplicationPipeline pipeline = ((IWebApplicationPipelineBuilder)application).Build();

        await ((IHost)application).StartAsync();
        rendering.ApplicationContext.IsRunning.ShouldBeTrue();
        TestHttpContext first = new("/report", HttpMethod.Get);
        TestHttpContext second = new("/report", HttpMethod.Get);
        await Task.WhenAll(pipeline.ExecuteAsync(first), pipeline.ExecuteAsync(second));
        first.ReadResponseBody().ShouldBe("<main>generated report</main>");
        second.ReadResponseBody().ShouldBe(first.ReadResponseBody());
        shells.ShouldBe(2);
        TestHttpContext skipped = new("/other", HttpMethod.Get);
        await pipeline.ExecuteAsync(skipped);
        shells.ShouldBe(2);
        await ((IHost)application).StopAsync();
        rendering.ApplicationContext.IsRunning.ShouldBeFalse();
        await ((IAsyncDisposable)application).DisposeAsync();
        await Should.ThrowAsync<ObjectDisposedException>(() => rendering.StartAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RenderScopesResolveWebServicesAndDisposeScopedServicesOnSuccessAndFailure(bool fail)
    {
        List<RequestService> requests = [];
        HostService hostService = new();
        ComponentRegistration component = ComponentRegistration.Define("scoped-report", new ComponentContract(), context =>
        {
            context.Services!.GetRequiredService<HostService>().ShouldBeSameAs(hostService);
            RequestService request = context.Services!.GetRequiredService<RequestService>();
            request.ShouldBeSameAs(context.Services!.GetRequiredService<RequestService>());
            return _ => fail ? throw new InvalidOperationException("render failed") : new TextNode(request.Id);
        });
        ComponentFactory components = new();
        components.Register(component);
        ServerRenderRegistry renders = new();
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(hostService).AddScoped<RequestService>(_ =>
        {
            RequestService request = new();
            requests.Add(request);
            return request;
        }).AddViuServerRendering(rendering =>
        {
            rendering.RootComponent = new ComponentNode(component.Reference);
            rendering.Components = components;
            rendering.ServerRenders = renders;
            rendering.ErrorHandler = static (exception, _, _) => throw exception;
        });
        await using WebApplication application = builder.Build();
        application.UseViuServerRenderer();
        IWebApplicationPipeline pipeline = ((IWebApplicationPipelineBuilder)application).Build();
        for (int index = 0; index < 2; index++)
        {
            TestHttpContext context = new("/", HttpMethod.Get);
            if (fail)
            {
                await Should.ThrowAsync<InvalidOperationException>(() => pipeline.ExecuteAsync(context));
                context.Response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            }
            else
            {
                await pipeline.ExecuteAsync(context);
                context.ReadResponseBody().ShouldBe(requests[index].Id);
            }
            requests[index].IsDisposed.ShouldBeTrue();
        }
        requests[0].ShouldNotBeSameAs(requests[1]);
        components.TryResolve(Report.Registration.Reference, out _).ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => renders.Register(new ServerRenderRegistration(
            component.Reference, static (_, _, _, _) => Task.CompletedTask)));
    }

    [Fact]
    public async Task EveryRenderOwnsFreshApplicationContextSsrContextAndServiceScope()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddViuServerRendering(rendering => rendering.UseRootComponent<Report>());
        await using WebApplication application = builder.Build();
        ViuApplicationServerRenderRequestScopeFactory scopes = application.Context.ServiceProvider
            .GetRequiredService<ViuServerRendering>().RequestScopes;
        ServerRenderRequest<IHttpContext> request = new(scopes.RootComponent, new TestHttpContext("/", HttpMethod.Get));
        await using IServerRenderRequestScope first = await scopes.CreateAsync(request);
        await using IServerRenderRequestScope second = await scopes.CreateAsync(request);
        first.Application.ShouldNotBeSameAs(second.Application);
        first.Application.Context.ShouldNotBeSameAs(second.Application.Context);
        first.RenderContext.ShouldNotBeSameAs(second.RenderContext);
        first.Application.Context.Services.ShouldNotBeSameAs(second.Application.Context.Services);
        first.Application.Context.WarnHandler.ShouldNotBeNull();
        first.Application.Context.ErrorHandler.ShouldNotBeNull();
    }

    [Fact]
    public void MissingRootFailsAtWebBuild()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddViuServerRendering(static _ => { });
        Should.Throw<InvalidOperationException>(builder.Build).Message.ShouldContain("root component");
    }

    [Fact]
    public async Task ExplicitCatalogsReplaceBothGeneratedDefaults()
    {
        ComponentFactory components = new();
        components.Register(ComponentRegistration.Define("ServerGeneratedReport", new ComponentContract(),
            static _ => static _ => new TextNode("explicit report")));
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddViuServerRendering(rendering =>
        {
            rendering.UseRootComponent<Report>();
            rendering.Components = components;
            rendering.ServerRenders = new ServerRenderRegistry();
        });
        await using WebApplication application = builder.Build();
        application.UseViuServerRenderer();
        IWebApplicationPipeline pipeline = ((IWebApplicationPipelineBuilder)application).Build();
        TestHttpContext context = new("/", HttpMethod.Get);

        await pipeline.ExecuteAsync(context);

        context.ReadResponseBody().ShouldBe("explicit report");
    }

    [Fact]
    public async Task CancelledRequestStillDisposesScopedServicesAndScopedState()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddScoped<RequestService>(static _ => new RequestService());
        builder.Services.AddScoped<IStateStoreRegistry>(static _ => new RequestState());
        builder.Services.AddViuServerRendering(rendering => rendering.UseRootComponent<Report>());
        await using WebApplication application = builder.Build();
        ViuApplicationServerRenderRequestScopeFactory scopes = application.Context.ServiceProvider
            .GetRequiredService<ViuServerRendering>().RequestScopes;
        ServerRenderRequest<IHttpContext> request = new(scopes.RootComponent, new TestHttpContext("/", HttpMethod.Get));
        using CancellationTokenSource cancellation = new();
        IServerRenderRequestScope scope = await scopes.CreateAsync(request, cancellation.Token);
        RequestService service = scope.Application.Context.Services!.GetRequiredService<RequestService>();
        RequestState state = scope.Application.Context.State.ShouldBeOfType<RequestState>();

        cancellation.Cancel();
        await scope.DisposeAsync(cancellation.Token);

        service.IsDisposed.ShouldBeTrue();
        state.IsDisposed.ShouldBeTrue();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await scopes.CreateAsync(request, cancellation.Token));
    }

    [Fact]
    public async Task PartialScopeCreationFailureReleasesAlreadyResolvedServices()
    {
        RequestService? created = null;
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddScoped<RequestService>(_ => created = new RequestService());
        builder.Services.AddScoped<IStateStoreRegistry>(services =>
        {
            services.GetRequiredService<RequestService>();
            throw new InvalidOperationException("state creation failed");
        });
        builder.Services.AddViuServerRendering(rendering => rendering.UseRootComponent<Report>());
        await using WebApplication application = builder.Build();
        ViuApplicationServerRenderRequestScopeFactory scopes = application.Context.ServiceProvider
            .GetRequiredService<ViuServerRendering>().RequestScopes;
        ServerRenderRequest<IHttpContext> request = new(scopes.RootComponent, new TestHttpContext("/", HttpMethod.Get));

        await Should.ThrowAsync<InvalidOperationException>(async () => await scopes.CreateAsync(request));

        created.ShouldNotBeNull().IsDisposed.ShouldBeTrue();
    }

    private sealed class Report : IComponentDefinition
    {
        public static ComponentRegistration Registration { get; } = ComponentRegistration.Define(
            "ServerGeneratedReport", new ComponentContract(), static _ => static _ => new TextNode("fallback report"));
    }

    private sealed class HostService;

    private sealed class RequestService : IAsyncDisposable
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public bool IsDisposed { get; private set; }
        public ValueTask DisposeAsync() { IsDisposed = true; return ValueTask.CompletedTask; }
    }

    private sealed class RequestState : IStateStoreRegistry
    {
        public int Count => 0;
        public bool IsDisposed { get; private set; }
        public TStore GetOrCreate<TStore>(StateStoreDefinition<TStore> definition) where TStore : class =>
            throw new NotSupportedException();
        public bool Remove<TStore>(StateStoreDefinition<TStore> definition) where TStore : class => false;
        public void Dispose() => IsDisposed = true;
    }

    private sealed class DocumentShell : IServerRenderDocumentShell
    {
        public ValueTask WritePrefixAsync(IServerRenderOutput output, CancellationToken cancellationToken = default) =>
            output.WriteAsync("<main>".AsMemory(), cancellationToken);
        public ValueTask WriteSuffixAsync(IServerRenderOutput output, IReadOnlyDictionary<string, string> teleports,
            CancellationToken cancellationToken = default) => output.WriteAsync("</main>".AsMemory(), cancellationToken);
    }
}
