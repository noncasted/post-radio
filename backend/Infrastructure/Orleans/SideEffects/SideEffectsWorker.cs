using Common;
using Common.Extensions;
using Common.Reactive;
using Infrastructure.Startup;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public class SideEffectsWorker : IHostedService
{
    public SideEffectsWorker(
        ISideEffectsStorage storage,
        ITransactions transactions,
        IOrleans orleans,
        IServiceLoopObserver loopObserver,
        ISideEffectsConfig config,
        IClusterFlags clusterFlags,
        IClusterParticipantContext participantContext,
        ILogger<SideEffectsWorker> logger)
    {
        _storage = storage;
        _transactions = transactions;
        _orleans = orleans;
        _loopObserver = loopObserver;
        _config = config;
        _clusterFlags = clusterFlags;
        _participantContext = participantContext;
        _logger = logger;
    }

    private readonly ISideEffectsStorage _storage;
    private readonly ITransactions _transactions;
    private readonly IOrleans _orleans;
    private readonly IServiceLoopObserver _loopObserver;
    private readonly ISideEffectsConfig _config;
    private readonly IClusterFlags _clusterFlags;
    private readonly IClusterParticipantContext _participantContext;
    private readonly ILogger<SideEffectsWorker> _logger;

    private int _inProgress;
    private readonly CancellationTokenSource _shutdownCts = new();
    private DateTime _lastStuckCheck = DateTime.MinValue;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var lifetime = cancellationToken.ToLifetime();
        Loop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    private async Task Loop(IReadOnlyLifetime lifetime)
    {
        await _loopObserver.IsOrleansStarted.WaitTrue(lifetime);
        await _participantContext.IsInitialized.WaitTrue(lifetime);

        while (lifetime.IsTerminated == false)
        {
            if (_clusterFlags.SideEffectsEnabled == false)
            {
                await Task.Delay(500, lifetime.Token);
                continue;
            }

            if (_shutdownCts.IsCancellationRequested)
                break;

            var foundWork = false;

            try
            {
                var options = _config.Value;
                var freeSlots = options.ConcurrentExecutions - _inProgress;

                if (freeSlots <= 0)
                {
                    await Task.Delay(_config.Value.EmptyScanDelay, lifetime.Token);
                    continue;
                }

                await _storage.RequeueReady();

                if ((DateTime.UtcNow - _lastStuckCheck).TotalSeconds >= options.StuckCheckIntervalSeconds)
                {
                    await _storage.RequeueStuckOlderThan(TimeSpan.FromMinutes(options.StuckThresholdMinutes));
                    _lastStuckCheck = DateTime.UtcNow;
                }

                var entries = await _storage.Read(freeSlots);
                foundWork = entries.Count > 0;
                BackendMetrics.SideEffectQueueDepth.Record(entries.Count);

                foreach (var entry in entries)
                {
                    if (_shutdownCts.IsCancellationRequested)
                        break;

                    ExecuteEntry(entry, lifetime).NoAwait();
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[SideEffects] Error in scan loop");
            }

            var delay = foundWork ? _config.Value.ScanDelay : _config.Value.EmptyScanDelay;
            await Task.Delay(delay, lifetime.Token);
        }
    }

    private async Task ExecuteEntry(SideEffectEntry entry, IReadOnlyLifetime lifetime)
    {
        using var activity = TraceExtensions.SideEffects.StartActivity("SideEffect.Execute");
        activity?.SetTag("side_effect.retry_count", entry.RetryCount);

        if (entry.Effect is ICorrelatedSideEffect correlated)
            activity?.SetTag("side_effect.correlation_id", correlated.CorrelationId);

        Interlocked.Increment(ref _inProgress);
        BackendMetrics.SideEffectInProgress.Add(1);
        using var watch = MetricWatch.Start(BackendMetrics.SideEffectDuration);

        try
        {
            if (entry.Effect is ITransactionalSideEffect)
            {
                var result = await _transactions
                                   .CreateBuilder(() => entry.Effect.Execute(_orleans))
                                   .WithCallback(npgsqlTransaction =>
                                       _storage.CompleteProcessing(npgsqlTransaction, entry.Id))
                                   .Run();

                if (!result.IsSuccess)
                    throw new Exception("[SideEffects] Transactional side effect failed.");
            }
            else
            {
                await entry.Effect.Execute(_orleans);
                await _storage.CompleteProcessing(entry.Id);
            }

            BackendMetrics.SideEffectProcessed.Add(1);
        }
        catch (Exception e)
        {
            var options = _config.Value;

            _logger.LogError(e,
                "[SideEffects] Effect {Id} failed (attempt {RetryCount}/{MaxRetry})",
                entry.Id, entry.RetryCount + 1, options.MaxRetryCount);

            if (entry.RetryCount > 0)
                BackendMetrics.SideEffectRetry.Add(1);

            BackendMetrics.SideEffectFailed.Add(1);

            try
            {
                await _storage.FailProcessing(entry.Id,
                    entry.RetryCount,
                    options.MaxRetryCount,
                    options.IncrementalRetryDelay,
                    e.Message);
            }
            catch (Exception failEx)
            {
                _logger.LogError(failEx, "[SideEffects] Failed to record failure for effect {Id}", entry.Id);
            }
        }
        finally
        {
            BackendMetrics.SideEffectInProgress.Add(-1);
            Interlocked.Decrement(ref _inProgress);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _shutdownCts.Cancel();

        while (Volatile.Read(ref _inProgress) > 0 && !cancellationToken.IsCancellationRequested)
            await Task.Delay(50, CancellationToken.None);

        if (Volatile.Read(ref _inProgress) > 0)
            _logger.LogWarning("[SideEffects] Shutdown timeout exceeded, {InProgress} effects still in progress",
                _inProgress);

        _shutdownCts.Dispose();
    }
}