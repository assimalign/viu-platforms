using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Cohesion.Web;
using Assimalign.Cohesion.Web.Hosting;

namespace Assimalign.Cohesion.Viu.DevServer;

internal static class DevServerRunner
{
    public static async Task RunAsync(
        DevServerOptions options,
        TextWriter output,
        string? aspNetCoreUrls,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);

        IReadOnlyList<DevServerAddress> addresses = DevServerAddressResolver.Resolve(options, aspNetCoreUrls);
        await using WebApplication application = DevServerApplicationFactory.Create(options, addresses);
        IWebApplicationServer server = application.Context.ServiceProvider.GetRequiredService<IWebApplicationServer>();

        await server.StartAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (DevServerAddress address in addresses)
            {
                await PortReadinessProbe.WaitAsync(address, cancellationToken).ConfigureAwait(false);
                await DevServerReadiness.WriteAsync(output, address.Url, cancellationToken).ConfigureAwait(false);
            }

            await WaitForCancellationAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await server.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static async Task WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
