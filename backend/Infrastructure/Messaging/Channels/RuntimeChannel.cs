using System.Collections.Concurrent;
using Common.Extensions;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;

namespace Infrastructure;

public interface IRuntimeChannelId
{
    string ToRaw();
}

public class RuntimeChannelId : IRuntimeChannelId
{
    public RuntimeChannelId(string id)
    {
        _id = id;
    }

    private readonly string _id;

    public string ToRaw()
    {
        return _id;
    }
}

[GenerateSerializer]
public class SequencedMessage
{
    [Id(0)] public long Sequence { get; set; }
    [Id(1)] public object Payload { get; set; } = null!;
}

[GenerateSerializer]
public class CatchUpResult
{
    [Id(0)] public IReadOnlyList<SequencedMessage> Messages { get; set; } = Array.Empty<SequencedMessage>();
    [Id(1)] public bool GapDetected { get; set; }
    [Id(2)] public long CurrentSequence { get; set; }
}

public interface IRuntimeChannel : IGrainWithStringKey
{
    Task AddObserver(Guid id, IRuntimeChannelObserver observer);
    Task RemoveObserver(Guid id);

    [AlwaysInterleave]
    Task Publish(object message);

    [AlwaysInterleave]
    Task<CatchUpResult> CatchUp(long lastSeenSequence);
}

public class RuntimeChannel : Grain, IRuntimeChannel
{
    public RuntimeChannel(ILogger<RuntimeChannel> logger, IRuntimeChannelConfig config)
    {
        _logger = logger;
        _config = config;
    }

    private readonly ILogger<RuntimeChannel> _logger;
    private readonly IRuntimeChannelConfig _config;
    private readonly ConcurrentDictionary<Guid, ObserverData> _observers = new();

    private long _sequenceNumber;
    private SequencedMessage?[] _buffer = null!;
    private int _bufferSize;

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _bufferSize = _config.Value.CatchUpBufferSize;
        _buffer = new SequencedMessage?[_bufferSize];
        return Task.CompletedTask;
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        var delay = MessagingGrainExtensions.GetKeepAliveDelay(_observers.Values, d => d.UpdateDate,
            _config.Value.ObserverKeepAliveMinutes);

        if (delay != null)
            DelayDeactivation(delay.Value);
        return Task.CompletedTask;
    }

    public Task AddObserver(Guid id, IRuntimeChannelObserver observer)
    {
        if (_observers.TryGetValue(id, out var data) == false)
        {
            data = new ObserverData
            {
                Observer = observer,
                UpdateDate = DateTime.UtcNow,
                Id = id
            };

            _observers[id] = data;
        }

        data.Observer = observer;
        data.UpdateDate = DateTime.UtcNow;

        return Task.CompletedTask;
    }

    public Task RemoveObserver(Guid id)
    {
        _observers.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public async Task Publish(object message)
    {
        using var activity = TraceExtensions.MessagingRuntimeChannel.StartActivity("RuntimeChannel.Publish");
        activity?.SetTag("message.type", message.GetType().Name);
        activity?.SetTag("observer.count", _observers.Count);

        BackendMetrics.ChannelPublished.Add(1);
        BackendMetrics.ChannelObserverCount.Record(_observers.Count);

        _sequenceNumber++;
        var sequenced = new SequencedMessage { Sequence = _sequenceNumber, Payload = message };
        _buffer[_sequenceNumber % _bufferSize] = sequenced;

        var toRemove = new ConcurrentBag<Guid>();

        await Task.WhenAll(_observers.Values.Select(data => SendSafe(data)));

        foreach (var id in toRemove)
            _observers.TryRemove(id, out _);

        return;

        async Task SendSafe(ObserverData data)
        {
            try
            {
                var timeout = TimeSpan.FromSeconds(_config.Value.DeliveryTimeoutSeconds);
                var deliveryTask = data.Observer.Send(sequenced);

                if (await Task.WhenAny(deliveryTask, Task.Delay(timeout)) != deliveryTask)
                {
                    toRemove.Add(data.Id);
                    BackendMetrics.ChannelDeliveryTimeout.Add(1);

                    _logger.LogWarning("[Messaging] [Channel] Delivery timeout on {ChannelName}",
                        this.GetPrimaryKeyString());
                    return;
                }

                await deliveryTask;
            }
            catch (Exception e)
            {
                toRemove.Add(data.Id);
                BackendMetrics.ChannelDeliveryFailure.Add(1);

                _logger.LogError(e,
                    "[Messaging] [Channel] Delivering message from {ChannelName} to observer failed",
                    this.GetPrimaryKeyString());
            }
        }
    }

    public Task<CatchUpResult> CatchUp(long lastSeenSequence)
    {
        if (lastSeenSequence >= _sequenceNumber)
        {
            return Task.FromResult(new CatchUpResult
            {
                CurrentSequence = _sequenceNumber
            });
        }

        var oldestInBuffer = _sequenceNumber - _bufferSize + 1;

        if (oldestInBuffer < 1)
            oldestInBuffer = 1;

        var gapDetected = lastSeenSequence < oldestInBuffer - 1;
        var startSequence = Math.Max(lastSeenSequence + 1, oldestInBuffer);

        var messages = new List<SequencedMessage>();

        for (var seq = startSequence; seq <= _sequenceNumber; seq++)
        {
            var entry = _buffer[seq % _bufferSize];

            if (entry != null && entry.Sequence == seq)
                messages.Add(entry);
        }

        if (messages.Count > 0)
        {
            BackendMetrics.ChannelCatchUpExecuted.Add(1);
            BackendMetrics.ChannelCatchUpMessages.Record(messages.Count);
        }

        if (gapDetected)
        {
            BackendMetrics.ChannelGapDetected.Add(1);

            _logger.LogWarning(
                "[Messaging] [Channel] Gap detected on {ChannelName}: requested seq {RequestedSeq}, oldest available {OldestSeq}",
                this.GetPrimaryKeyString(), lastSeenSequence, oldestInBuffer);
        }

        return Task.FromResult(new CatchUpResult
        {
            Messages = messages,
            GapDetected = gapDetected,
            CurrentSequence = _sequenceNumber
        });
    }

    public class ObserverData
    {
        public required Guid Id { get; init; }
        public required IRuntimeChannelObserver Observer { get; set; }
        public required DateTime UpdateDate { get; set; }
    }
}