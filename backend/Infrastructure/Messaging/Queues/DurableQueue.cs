using Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IDurableQueue : IGrainWithStringKey
{
    Task AddObserver(Guid id, IDurableQueueObserver observer);
    Task RemoveObserver(Guid id);
    Task Push(object message);
}

public class DurableQueue : Grain, IDurableQueue
{
    public DurableQueue(ILogger<DurableQueue> logger, IDurableQueueConfig config)
    {
        _logger = logger;
        _config = config;
    }

    private readonly ILogger<DurableQueue> _logger;
    private readonly IDurableQueueConfig _config;

    private readonly Dictionary<Guid, ObserverData> _observers = new();

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        var delay = MessagingGrainExtensions.GetKeepAliveDelay(_observers.Values, d => d.UpdateDate,
            _config.Value.ObserverKeepAliveMinutes);

        if (delay != null)
            DelayDeactivation(delay.Value);
        return Task.CompletedTask;
    }

    public Task AddObserver(Guid id, IDurableQueueObserver observer)
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
        _observers.Remove(id);
        return Task.CompletedTask;
    }

    public async Task Push(object message)
    {
        using var activity = TraceExtensions.MessagingDurableQueue.StartActivity("DurableQueue.Push");
        activity?.SetTag("message.type", message.GetType().Name);
        activity?.SetTag("observer.count", _observers.Count);

        BackendMetrics.DurableQueuePushed.Add(1);
        BackendMetrics.DurableQueueObserverCount.Record(_observers.Count);

        if (_observers.Count == 0)
        {
            BackendMetrics.DurableQueueNoSubscribers.Add(1);

            _logger.LogWarning("[Messaging] [DurableQueue] No active subscribers for queue '{QueueName}'",
                this.GetPrimaryKeyString());

            throw new InvalidOperationException(
                $"No active subscribers for durable queue '{this.GetPrimaryKeyString()}'. Message left in processing for requeue.");
        }

        List<Guid>? toRemove = null;

        foreach (var data in _observers.Values)
        {
            try
            {
                await data.Observer.Send(message);
            }
            catch (Exception e)
            {
                toRemove ??= new List<Guid>();
                toRemove.Add(data.Id);
                BackendMetrics.DurableQueueDeliveryFailure.Add(1);

                _logger.LogError(e,
                    "[Messaging] [DurableQueue] Delivering message from {QueueName} to observer failed",
                    this.GetPrimaryKeyString());
            }
        }

        if (toRemove != null)
        {
            foreach (var id in toRemove)
                _observers.Remove(id);

            if (_observers.Count == 0)
            {
                BackendMetrics.DurableQueueNoSubscribers.Add(1);

                _logger.LogWarning(
                    "[Messaging] [DurableQueue] All subscribers removed after delivery failure on queue '{QueueName}'",
                    this.GetPrimaryKeyString());
            }
        }
    }

    public class ObserverData
    {
        public required Guid Id { get; init; }
        public required IDurableQueueObserver Observer { get; set; }
        public required DateTime UpdateDate { get; set; }
    }
}