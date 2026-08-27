using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Cohesion.Web;

namespace Assimalign.Viu.Cohesion.DevServer;

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
        IWebApplicationPipeline pipeline = DevServerPipelineFactory.Create(options);

        await using TrackedHttpServer server = new(pipeline, addresses);
        await server.StartAsync(cancellationToken).ConfigureAwait(false);

        foreach (DevServerAddress address in addresses)
        {
            await DevServerReadiness.WriteAsync(output, address.Url, cancellationToken).ConfigureAwait(false);
        }

        Task cancellation = WaitForCancellationAsync(cancellationToken);
        Task completed = await Task.WhenAny(server.Completion, cancellation).ConfigureAwait(false);
        if (completed == server.Completion)
        {
            await server.Completion.ConfigureAwait(false);
            throw new InvalidOperationException("All HTTP listener loops stopped unexpectedly.");
        }

        await server.StopAsync().ConfigureAwait(false);
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
