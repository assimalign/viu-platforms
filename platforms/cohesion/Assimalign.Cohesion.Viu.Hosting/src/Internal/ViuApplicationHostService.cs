using System;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Cohesion.Hosting;
using Assimalign.Viu;

namespace Assimalign.Cohesion.Viu.Hosting;

internal sealed class ViuApplicationHostService : IHostService
{
    private readonly IApplication _application;
    private readonly ViuApplicationContext _hostContext;
    private CancellationTokenRegistration _stoppingRegistration;

    public ViuApplicationHostService(
        IApplication application,
        ViuApplicationContext hostContext)
    {
        _application = application;
        _hostContext = hostContext;
    }

    public ServiceId Id { get; } = ServiceId.New();

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _stoppingRegistration = _application.Context.Stopping.Register(
            static state => ((ViuApplicationContext)state!).Shutdown(),
            _hostContext);

        try
        {
            await _application.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _stoppingRegistration.Dispose();
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _stoppingRegistration.Dispose();
        await _application.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
