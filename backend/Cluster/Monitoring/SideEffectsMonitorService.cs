using System.Diagnostics.Metrics;
using Cluster.Coordination;
using Cluster.Discovery;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Cluster.Monitoring;

public class SideEffectsMonitorService : ILocalSetupCompleted
{
    public SideEffectsMonitorService(
        ISideEffectsStorage storage,
        IMessaging messaging,
        IServiceDiscovery discovery,
        ILogger<SideEffectsMonitorService> logger)
    {
        _storage = storage;
        _messaging = messaging;
        _discovery = discovery;
        _logger = logger;
    }

    private readonly ISideEffectsStorage _storage;
    private readonly IMessaging _messaging;
    private readonly IServiceDiscovery _discovery;
    private readonly ILogger<SideEffectsMonitorService> _logger;

    private readonly ViewableProperty<SideEffectsLiveData> _current = new(new SideEffectsLiveData());

    private long _processedAccumulator;
    private long _failedAccumulator;
    private readonly List<SideEffectsThroughputEntry> _history = new();
    private const int MaxHistorySize = 120;

    public Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        var listener = new MeterListener();

        listener.InstrumentPublished = (instrument, meterListener) => {
            if (instrument.Meter.Name == "Backend" &&
                instrument.Name is "backend.side_effects.processed" or "backend.side_effects.failed")
                meterListener.EnableMeasurementEvents(instrument);
        };

        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) => {
            if (instrument.Name == "backend.side_effects.processed")
                Interlocked.Add(ref _processedAccumulator, value);
            else if (instrument.Name == "backend.side_effects.failed")
                Interlocked.Add(ref _failedAccumulator, value);
        });

        listener.Start();
        lifetime.Listen(() => listener.Dispose());

        Loop(lifetime).NoAwait();

        _messaging.AddPipeRequestHandler<SideEffectsSnapshotRequest, SideEffectsSnapshotResponse>(
            lifetime,
            new MessagePipeServiceRequestId(_discovery.Self, typeof(SideEffectsSnapshotRequest)),
            _ => Task.FromResult(new SideEffectsSnapshotResponse {
                ServiceTag = _discovery.Self.Tag.ToString(),
                ServiceId = _discovery.Self.Id,
                Data = _current.Value,
            }));

        return Task.CompletedTask;
    }

    private async Task Loop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            try
            {
                var processed = Interlocked.Exchange(ref _processedAccumulator, 0);
                var failed = Interlocked.Exchange(ref _failedAccumulator, 0);

                _history.Add(new SideEffectsThroughputEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Processed = processed,
                    Failed = failed
                });

                if (_history.Count > MaxHistorySize)
                    _history.RemoveAt(0);

                var stats = await _storage.GetStats();
                var retryEntries = await _storage.GetRetryEntries(50);

                _current.Set(new SideEffectsLiveData
                {
                    QueueCount = stats.QueueCount,
                    ProcessingCount = stats.ProcessingCount,
                    RetryCount = stats.RetryCount,
                    DeadLetterCount = stats.DeadLetterCount,
                    ThroughputHistory = _history.ToList(),
                    RetryEntries = retryEntries.Select(e => new SideEffectsRetryEntry
                    {
                        Id = e.Id,
                        TypeName = e.TypeName,
                        RetryCount = e.RetryCount,
                        RetryAfter = e.RetryAfter,
                        CreatedAt = e.CreatedAt
                    }).ToList()
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[SideEffectsMonitor] Failed to update live data");
            }

            await Task.Delay(5000, lifetime.Token);
        }
    }
}
