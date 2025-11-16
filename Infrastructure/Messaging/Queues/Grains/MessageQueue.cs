using Common;
using Infrastructure.StorableActions;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging;

public class MessageQueue : BatchWriter<MessageQueueState, object>, IMessageQueue
{
    public MessageQueue(
        [States.MessageQueue] IPersistentState<MessageQueueState> state,
        ILogger<MessageQueue> logger) : base(state)
    {
        _logger = logger;
    }

    private readonly ILogger<MessageQueue> _logger;

    private readonly Dictionary<Guid, ObserverData> _observers = new();

    protected override BatchWriterOptions Options { get; } = new()
    {
        RequiresTransaction = false
    };

    public Task AddObserver(Guid id, IMessageQueueObserver observer)
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

    public Task PushDirect(object message)
    {
        return WriteDirect(message);
    }

    public Task PushTransactional(object message)
    {
        return WriteTransactional(message);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        var latestUpdate = _observers.Values.Max(t => t.UpdateDate);
        var timeSinceLastUpdate = DateTime.UtcNow - latestUpdate;

        if (timeSinceLastUpdate > TimeSpan.FromMinutes(3))
            return;

        throw new Exception("[Messaging] [Queue] Keeping queue alive because observer was recently set");
    }

    protected override async Task Process(IReadOnlyList<object> entries)
    {
        var toRemove = new List<Guid>();

        await Task.WhenAll(
            _observers.Values.Select(data =>
                {
                    var observer = data.Observer;

                    try
                    {
                        return observer.Send(entries);
                    }
                    catch (Exception e)
                    {
                        toRemove.Add(data.Id);

                        _logger.LogError(
                            e,
                            "[Messaging] [Queue] Delevering message from {QueueName} to observer failed",
                            StringId
                        );

                        return Task.CompletedTask;
                    }
                }
            )
        );

        foreach (var observer in toRemove)
            _observers.Remove(observer);
    }

    public class ObserverData
    {
        public required Guid Id { get; init; }
        public required IMessageQueueObserver Observer { get; set; }
        public required DateTime UpdateDate { get; set; }
    }
}