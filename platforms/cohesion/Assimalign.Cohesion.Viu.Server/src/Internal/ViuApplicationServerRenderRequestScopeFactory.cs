using System;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Cohesion.Http;
using Assimalign.Cohesion.Viu.Hosting;
using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.ServerRenderer;
using Assimalign.Viu.State;

namespace Assimalign.Cohesion.Viu.Server;

internal sealed class ViuApplicationServerRenderRequestScopeFactory :
    IServerRenderRequestScopeFactory<IHttpContext>
{
    private readonly ApplicationOptions _composition;
    private readonly bool _createServiceScope;

    public ViuApplicationServerRenderRequestScopeFactory(ViuApplication application)
        : this((application ?? throw new ArgumentNullException(nameof(application)))
            .CreateApplicationOptionsSnapshot(), createServiceScope: false)
    {
    }

    internal ViuApplicationServerRenderRequestScopeFactory(ApplicationOptions composition, bool createServiceScope)
    {
        _composition = composition;
        _createServiceScope = createServiceScope;
        RootComponent = _composition.RootComponent
            ?? throw new InvalidOperationException(
                "The hosted Viu application does not have a root component.");
    }

    public VirtualNode RootComponent { get; }

    public async ValueTask<IServerRenderRequestScope> CreateAsync(
        ServerRenderRequest<IHttpContext> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        AsyncServiceScope? serviceScope = _createServiceScope
            ? _composition.Services!.CreateAsyncScope()
            : null;
        try
        {
            IServiceProvider? services = serviceScope?.ServiceProvider ?? _composition.Services;
            ApplicationOptions requestOptions = new()
            {
                RootComponent = request.RootComponent,
                Components = _composition.Components,
                Services = services,
                State = _composition.State ?? services?.GetService<IStateStoreRegistry>(),
                Directives = _composition.Directives ?? services?.GetService<IDirectiveResolver>(),
                ErrorHandler = _composition.ErrorHandler,
                WarnHandler = _composition.WarnHandler,
                EventObserver = _composition.EventObserver,
            };
            var application = new ServerRenderApplication(
                new ApplicationContext(requestOptions));
            IServerRenderRequestScope scope = new ViuApplicationServerRenderRequestScope(
                application,
                new SsrContext(),
                serviceScope);
            return scope;
        }
        catch
        {
            if (serviceScope is AsyncServiceScope ownedScope)
            {
                await ownedScope.DisposeAsync().ConfigureAwait(false);
            }
            throw;
        }
    }

    private sealed class ViuApplicationServerRenderRequestScope :
        IServerRenderRequestScope
    {
        private readonly AsyncServiceScope? _serviceScope;
        private bool _isDisposed;

        public ViuApplicationServerRenderRequestScope(
            ServerRenderApplication application,
            SsrContext renderContext,
            AsyncServiceScope? serviceScope)
        {
            Application = application;
            RenderContext = renderContext;
            _serviceScope = serviceScope;
        }

        public ServerRenderApplication Application { get; }

        public SsrContext RenderContext { get; }

        public ValueTask DisposeAsync() => DisposeAsync(CancellationToken.None);

        public ValueTask DisposeAsync(CancellationToken cancellationToken)
        {
            if (_isDisposed)
            {
                return ValueTask.CompletedTask;
            }
            _isDisposed = true;
            // Cleanup must finish even when the HTTP request has already been cancelled.
            return _serviceScope?.DisposeAsync() ?? ValueTask.CompletedTask;
        }
    }
}
