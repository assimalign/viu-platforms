using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Cohesion.Http;
using Assimalign.Cohesion.Http.Transports;
using Assimalign.Cohesion.Transports;
using Assimalign.Cohesion.Web;

namespace Assimalign.Viu.Cohesion.DevServer;

internal sealed class TrackedHttpServer : IAsyncDisposable
{
    private readonly IReadOnlyList<DevServerAddress> _addresses;
    private readonly IWebApplicationPipeline _pipeline;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly List<IHttpConnectionListener> _listeners = [];
    private readonly ConcurrentDictionary<long, Task> _connections = new();

    private Task _completion = Task.CompletedTask;
    private long _connectionKey;
    private int _started;
    private int _stopped;

    public TrackedHttpServer(IWebApplicationPipeline pipeline, IReadOnlyList<DevServerAddress> addresses)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(addresses);
        ArgumentOutOfRangeException.ThrowIfZero(addresses.Count);

        _pipeline = pipeline;
        _addresses = addresses;
    }

    public Task Completion => _completion;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            throw new InvalidOperationException("The development server has already been started.");
        }

        foreach (DevServerAddress address in _addresses)
        {
            IHttpConnectionListener listener = HttpConnectionListener.Create(options =>
            {
                options.UseHttp1(transport =>
                {
                    transport.EndPoint = address.EndPoint;
                    transport.NoDelay = true;
                });
            });
            _listeners.Add(listener);
        }

        CancellationToken shutdownToken = _shutdown.Token;
        Task[] acceptLoops = _listeners
            .Select(listener => Task.Run(() => AcceptLoopAsync(listener, shutdownToken), CancellationToken.None))
            .ToArray();
        _completion = MonitorAcceptLoopsAsync(acceptLoops, shutdownToken);

        try
        {
            for (int index = 0; index < _addresses.Count; index++)
            {
                await PortReadinessProbe
                    .WaitAsync(_addresses[index], acceptLoops[index], cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch
        {
            try
            {
                await StopAsync().ConfigureAwait(false);
            }
            catch
            {
                // Preserve the startup or binding failure that entered this cleanup path.
            }

            throw;
        }
    }

    public async Task StopAsync()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
        {
            return;
        }

        _shutdown.Cancel();

        foreach (IHttpConnectionListener listener in _listeners)
        {
            await listener.DisposeAsync().ConfigureAwait(false);
        }

        try
        {
            await _completion.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        Task[] activeConnections = _connections.Values.ToArray();
        if (activeConnections.Length > 0)
        {
            await Task.WhenAll(activeConnections).ConfigureAwait(false);
        }

        _shutdown.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return new ValueTask(StopAsync());
    }

    private async Task AcceptLoopAsync(IHttpConnectionListener listener, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                IHttpConnection connection = await listener.AcceptOrListenAsync(cancellationToken).ConfigureAwait(false);
                long key = Interlocked.Increment(ref _connectionKey);
                Task connectionTask = ServeConnectionAsync(connection, key, cancellationToken);
                _connections[key] = connectionTask;

                if (connectionTask.IsCompleted)
                {
                    _connections.TryRemove(key, out _);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static async Task MonitorAcceptLoopsAsync(Task[] acceptLoops, CancellationToken cancellationToken)
    {
        Task firstCompleted = await Task.WhenAny(acceptLoops).ConfigureAwait(false);
        if (!cancellationToken.IsCancellationRequested)
        {
            await firstCompleted.ConfigureAwait(false);
            throw new InvalidOperationException("An HTTP listener loop stopped unexpectedly.");
        }

        await Task.WhenAll(acceptLoops).ConfigureAwait(false);
    }

    private async Task ServeConnectionAsync(
        IHttpConnection connection,
        long key,
        CancellationToken cancellationToken)
    {
        try
        {
            await using (connection.ConfigureAwait(false))
            {
                IHttpConnectionContext connectionContext = await connection
                    .OpenAsync(cancellationToken)
                    .ConfigureAwait(false);

                try
                {
                    await foreach (IHttpContext context in connectionContext
                        .ReceiveAsync(cancellationToken)
                        .ConfigureAwait(false))
                    {
                        try
                        {
                            await _pipeline.InvokeAsync(context, cancellationToken).ConfigureAwait(false);
                            await connectionContext.SendAsync(context, cancellationToken).ConfigureAwait(false);
                        }
                        finally
                        {
                            await context.DisposeAsync().ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
                catch
                {
                    connection.Abort();
                }
                finally
                {
                    await DisposeConnectionContextAsync(connectionContext).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            // Opening or disposing a broken peer must not surface as an unobserved task fault.
        }
        finally
        {
            _connections.TryRemove(key, out _);
        }
    }

    private static async ValueTask DisposeConnectionContextAsync(IHttpConnectionContext context)
    {
        switch (context)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }
}
