using System;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Cohesion.Hosting;
using Assimalign.Cohesion.Http;
using Assimalign.Cohesion.Web;
using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.ServerRenderer;

namespace Assimalign.Cohesion.Viu.Server;

internal sealed class ViuServerRendering : IHostService, IAsyncDisposable
{
    private readonly ServerViuApplication _application;
    private readonly ServerRenderAdaptor<IHttpContext> _adaptor;
    private readonly Func<IHttpContext, IServerRenderDocumentShell>? _documentShell;

    public ViuServerRendering(IServiceProvider services, ViuServerRenderingOptions options)
    {
        ApplicationOptions composition = new()
        {
            RootComponent = options.RootComponent ?? throw new InvalidOperationException(
                "AddViuServerRendering requires a root component. Set RootComponent or call UseRootComponent<TComponent>()."),
            Components = options.Components ?? services.GetRequiredService<IComponentFactory>(),
            Services = services,
            State = options.State,
            Directives = options.Directives,
            WarnHandler = options.WarnHandler ?? (static message => Console.Error.WriteLine($"Viu warning: {message}")),
            ErrorHandler = options.ErrorHandler ?? (static (exception, _, source) =>
                Console.Error.WriteLine($"Viu error ({source}): {exception}")),
            EventObserver = options.EventObserver,
        };
        ServerRenderRegistry renders = options.ServerRenders ?? new ServerRenderRegistry();
        if (options.ServerRenders is null)
        {
            renders.RegisterGeneratedRenders();
        }

        RequestScopes = new ViuApplicationServerRenderRequestScopeFactory(composition, createServiceScope: true);
        _adaptor = new ServerRenderAdaptor<IHttpContext>(RequestScopes, renders.Freeze());
        _documentShell = options.DocumentShell;
        _application = new ServerViuApplication(new ApplicationContext(composition));
    }

    public ServiceId Id { get; } = ServiceId.New();

    internal ViuApplicationServerRenderRequestScopeFactory RequestScopes { get; }

    internal IApplicationContext ApplicationContext => _application.Context;

    public Task StartAsync(CancellationToken cancellationToken = default) =>
        _application.StartAsync(cancellationToken).AsTask();

    public Task StopAsync(CancellationToken cancellationToken = default) =>
        _application.StopAsync(cancellationToken).AsTask();

    public ValueTask DisposeAsync() => _application.DisposeAsync();

    public IWebApplicationMiddleware CreateMiddleware(Func<IHttpContext, bool>? shouldRender) =>
        _documentShell is null
            ? new ViuServerRendererMiddleware(_adaptor, _ => RequestScopes.RootComponent, shouldRender ?? (static _ => true))
            : new ViuServerRendererMiddleware(_adaptor, _ => RequestScopes.RootComponent, _documentShell, shouldRender ?? (static _ => true));
}
