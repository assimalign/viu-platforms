using System;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Cohesion.Http;
using Assimalign.Cohesion.Viu.Hosting;
using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.ServerRenderer;

namespace Assimalign.Cohesion.Viu.Server;

internal sealed class ViuApplicationServerRenderRequestScopeFactory :
    IServerRenderRequestScopeFactory<IHttpContext>
{
    private readonly ApplicationOptions _composition;

    public ViuApplicationServerRenderRequestScopeFactory(ViuApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        _composition = application.CreateApplicationOptionsSnapshot();
        RootComponent = _composition.RootComponent
            ?? throw new InvalidOperationException(
                "The hosted Viu application does not have a root component.");
    }

    public VirtualNode RootComponent { get; }

    public ValueTask<IServerRenderRequestScope> CreateAsync(
        ServerRenderRequest<IHttpContext> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        ApplicationOptions requestOptions = new()
        {
            RootComponent = request.RootComponent,
            Components = _composition.Components,
            Services = _composition.Services,
            State = _composition.State,
            Directives = _composition.Directives,
            ErrorHandler = _composition.ErrorHandler,
            WarnHandler = _composition.WarnHandler,
            EventObserver = _composition.EventObserver,
        };
        var application = new ServerRenderApplication(
            new ApplicationContext(requestOptions));
        IServerRenderRequestScope scope = new ViuApplicationServerRenderRequestScope(
            application,
            new SsrContext());
        return ValueTask.FromResult(scope);
    }

    private sealed class ViuApplicationServerRenderRequestScope :
        IServerRenderRequestScope
    {
        public ViuApplicationServerRenderRequestScope(
            ServerRenderApplication application,
            SsrContext renderContext)
        {
            Application = application;
            RenderContext = renderContext;
        }

        public ServerRenderApplication Application { get; }

        public SsrContext RenderContext { get; }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public ValueTask DisposeAsync(CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }
}
