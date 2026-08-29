using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu;

namespace Assimalign.Cohesion.Viu.Server;

internal sealed class ServerViuApplication : IApplication
{
    private readonly List<ApplicationMiddleware> _middleware = [];
    private readonly TaskCompletionSource<object?> _startupCompletion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ApplicationContext _context;
    private readonly ApplicationLifetime _lifetime;
    private CancellationTokenRegistration _startCancellationRegistration;
    private Task? _pipelineExecution;
    private Task? _stopExecution;
    private bool _isDisposed;

    public ServerViuApplication(ApplicationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _lifetime = new ApplicationLifetime(context);
    }

    public IApplicationContext Context => _context;

    public IApplication Use(ApplicationMiddleware middleware)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(middleware);
        if (_lifetime.State != ApplicationState.Created)
        {
            throw new InvalidOperationException(
                "Application middleware must be registered before execution begins.");
        }

        _middleware.Add(middleware);
        return this;
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        _lifetime.StartExecution();
        _startCancellationRegistration = RegisterStopping(cancellationToken);
        _pipelineExecution = ExecutePipelineAsync();
        return new ValueTask(_startupCompletion.Task);
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        Task stopExecution = GetOrCreateStopExecution();
        return cancellationToken.CanBeCanceled
            ? new ValueTask(stopExecution.WaitAsync(cancellationToken))
            : new ValueTask(stopExecution);
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        try
        {
            await GetOrCreateStopExecution().ConfigureAwait(false);
        }
        finally
        {
            _startCancellationRegistration.Dispose();
            _lifetime.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    private async Task ExecutePipelineAsync()
    {
        await Task.Yield();
        ApplicationDelegate pipeline = ExecuteTerminalAsync;
        for (int index = _middleware.Count - 1; index >= 0; index--)
        {
            ApplicationMiddleware middleware = _middleware[index];
            ApplicationDelegate next = pipeline;
            pipeline = context => middleware(context, next);
        }

        try
        {
            await pipeline(Context).ConfigureAwait(false);
            _lifetime.CompleteStopping();
            _startupCompletion.TrySetResult(null);
        }
        catch (OperationCanceledException exception)
            when (_lifetime.IsStoppingCancellation(exception))
        {
            _lifetime.CompleteStopping();
            _startupCompletion.TrySetResult(null);
        }
        catch (Exception exception)
        {
            _lifetime.Fail(exception);
            _startupCompletion.TrySetException(exception);
            throw;
        }
        finally
        {
            _startCancellationRegistration.Dispose();
        }
    }

    private async ValueTask ExecuteTerminalAsync(IApplicationContext context)
    {
        context.Stopping.ThrowIfCancellationRequested();
        _lifetime.SignalRunning();
        _startupCompletion.TrySetResult(null);
        await WaitForStoppingAsync(context.Stopping).ConfigureAwait(false);
    }

    private Task GetOrCreateStopExecution()
    {
        if (_stopExecution is not null)
        {
            return _stopExecution;
        }

        if (_lifetime.State is ApplicationState.Starting or ApplicationState.Running)
        {
            _lifetime.RequestStopping();
        }

        if (_pipelineExecution is null)
        {
            return Task.CompletedTask;
        }

        _stopExecution = _pipelineExecution;
        return _stopExecution;
    }

    private CancellationTokenRegistration RegisterStopping(CancellationToken cancellationToken)
    {
        return cancellationToken.CanBeCanceled
            ? cancellationToken.Register(
                static state => ((ServerViuApplication)state!).RequestStopping(),
                this)
            : default;
    }

    private void RequestStopping() => _lifetime.RequestStopping();

    private static async Task WaitForStoppingAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
