using System.Diagnostics;
using Common.Extensions;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;

namespace Infrastructure;

public interface IRuntimePipe : IGrainWithStringKey
{
    Task BindObserver(IRuntimePipeObserver observer);
    Task<TResponse> Send<TResponse>(object message);
    Task<bool> HasObserver();
}

[Reentrant]
public class RuntimePipe : Grain, IRuntimePipe
{
    public RuntimePipe(ILogger<RuntimePipe> logger, IRuntimePipeConfig config)
    {
        _logger = logger;
        _config = config;
    }

    private readonly ILogger<RuntimePipe> _logger;
    private readonly IRuntimePipeConfig _config;

    private IRuntimePipeObserver? _observer;
    private DateTime _setDate;

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        if (_observer != null)
        {
            var timeSinceLastUpdate = DateTime.UtcNow - _setDate;
            var keepAlive = TimeSpan.FromMinutes(_config.Value.ObserverKeepAliveMinutes);

            if (timeSinceLastUpdate < keepAlive)
                DelayDeactivation(keepAlive - timeSinceLastUpdate);
        }

        return Task.CompletedTask;
    }

    public Task BindObserver(IRuntimePipeObserver observer)
    {
        _logger.LogTrace("[Messaging] [RuntimePipe] Binding observer to pipe {PipeId}", this.GetPrimaryKeyString());
        _observer = observer;
        _setDate = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task<bool> HasObserver()
    {
        return Task.FromResult(_observer != null);
    }

    public async Task<TResponse> Send<TResponse>(object message)
    {
        using var activity = TraceExtensions.MessagingRuntimePipe.StartActivity("RuntimePipe.Send");
        activity?.SetTag("message.type", message.GetType().Name);
        activity?.SetTag("pipe.timeout", _config.Value.SendTimeoutSeconds);

        BackendMetrics.PipeRequestSent.Add(1);
        using var watch = MetricWatch.Start(BackendMetrics.PipeDuration);

        _logger.LogTrace(
            "[Messaging] [RuntimePipe] Sending request-response message {MessageType} expecting {ResponseType} to pipe {PipeId}",
            message.GetType().Name, typeof(TResponse).Name, this.GetPrimaryKeyString());

        var observer = _observer;

        if (observer == null)
        {
            _logger.LogError(
                "[Messaging] [RuntimePipe] No observer bound for request-response message {MessageType} on pipe {PipeId}",
                message.GetType().Name, this.GetPrimaryKeyString());
            throw new InvalidOperationException($"No observer bound for pipe {this.GetPrimaryKeyString()}");
        }

        try
        {
            var timeout = TimeSpan.FromSeconds(_config.Value.SendTimeoutSeconds);
            var response = await observer.Send<TResponse>(message).WaitAsync(timeout);

            _logger.LogTrace(
                "[Messaging] [RuntimePipe] Successfully received response {ResponseType} for message {MessageType} on pipe {PipeId}",
                typeof(TResponse).Name, message.GetType().Name, this.GetPrimaryKeyString());
            return response;
        }
        catch (TimeoutException ex)
        {
            DiscardObserverIfSame(observer);

            activity?.SetStatus(ActivityStatusCode.Error, "Pipe send timed out");
            BackendMetrics.PipeTimeout.Add(1);

            _logger.LogError(ex,
                "[Messaging] [RuntimePipe] Failed to process request-response message {MessageType} on pipe {PipeId}",
                message.GetType().Name, this.GetPrimaryKeyString());
            throw;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            _logger.LogError(ex,
                "[Messaging] [RuntimePipe] Failed to process request-response message {MessageType} on pipe {PipeId}",
                message.GetType().Name, this.GetPrimaryKeyString());
            throw;
        }
    }

    private void DiscardObserverIfSame(IRuntimePipeObserver observer)
    {
        if (ReferenceEquals(_observer, observer) == false)
            return;

        _observer = null;

        _logger.LogWarning("[Messaging] [RuntimePipe] Discarded stale observer for pipe {PipeId} after send failure",
            this.GetPrimaryKeyString());
    }
}