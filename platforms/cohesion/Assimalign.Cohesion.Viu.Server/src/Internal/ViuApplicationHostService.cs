using System;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Cohesion.Hosting;
using Assimalign.Cohesion.Viu.Hosting;

namespace Assimalign.Cohesion.Viu.Server;

internal sealed class ViuApplicationHostService : IHostService
{
    private readonly ViuApplication _application;

    public ViuApplicationHostService(ViuApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        _application = application;
    }

    public ServiceId Id { get; } = ServiceId.New();

    public Task StartAsync(CancellationToken cancellationToken = default) =>
        _application.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = default) =>
        _application.StopAsync(cancellationToken);
}
