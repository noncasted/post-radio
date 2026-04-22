using System.Collections.Concurrent;
using Common.Extensions;
using Common.Reactive;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IDurableQueueClient
{
    Task Start(IReadOnlyLifetime lifetime);

    Task<IViewableDelegate<T>> GetOrCreateConsumer<T>(IDurableQueueId id);
    void PushTransactional(IDurableQueueId id, object message);
    Task PushDirect(IDurableQueueId id, object message);
}

public class DurableQueueClient : IDurableQueueClient
{
    public DurableQueueClient(
        IOrleans orleans,
        ISideEffectsStorage sideEffectsStorage,
        ILogger<DurableQueueClient> logger)
    {
        _orleans = orleans;
        _sideEffectsStorage = sideEffectsStorage;
        _logger = logger;
    }

    private readonly IOrleans _orleans;
    private readonly ISideEffectsStorage _sideEffectsStorage;
    private readonly ILogger<DurableQueueClient> _logger;

    private readonly ConcurrentDictionary<string, Listener> _listeners = new();
    private readonly SemaphoreSlim _createLock = new(1, 1);

    public Task Start(IReadOnlyLifetime lifetime)
    {
        ResubscribeLoop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    public async Task<IViewableDelegate<T>> GetOrCreateConsumer<T>(IDurableQueueId id)
    {
        var rawId = id.ToRaw();

        if (_listeners.TryGetValue(rawId, out var existing))
            return (ViewableDelegate<T>)existing.Delegate;

        await _createLock.WaitAsync();

        try
        {
            if (_listeners.TryGetValue(rawId, out existing))
                return (ViewableDelegate<T>)existing.Delegate;

            var source = new ViewableDelegate<T>();

            var observer = new DurableQueueObserver(message => {
                if (message is not T castedMessage)
                    throw new InvalidCastException();

                source.Invoke(castedMessage);
            });

            var observerReference = _orleans.Client.CreateObjectReference<IDurableQueueObserver>(observer);

            var listener = new Listener
            {
                Id = id,
                ObserverSource = observer,
                ObserverReference = observerReference,
                Queue = GetQueue(id),
                Logger = _logger,
                Delegate = source,
                Orleans = _orleans
            };

            _listeners[rawId] = listener;
            await listener.Resubscribe();

            return source;
        }
        finally
        {
            _createLock.Release();
        }
    }

    public void RemoveConsumer(IDurableQueueId id)
    {
        var rawId = id.ToRaw();

        if (_listeners.TryRemove(rawId, out var listener))
            listener.Cleanup();
    }

    public void PushTransactional(IDurableQueueId id, object message)
    {
        if (TransactionContextProvider.Current == null)
            throw new InvalidOperationException();

        var sideEffect = new DurableQueueSideEffect()
        {
            QueueName = id.ToRaw(),
            Message = message,
            CorrelationId = TransactionContextProvider.Current?.Id ?? Guid.NewGuid()
        };

        sideEffect.AddToTransaction();
    }

    public Task PushDirect(IDurableQueueId id, object message)
    {
        return _sideEffectsStorage.Write(new DurableQueueSideEffect()
        {
            QueueName = id.ToRaw(),
            Message = message,
            CorrelationId = Guid.NewGuid()
        });
    }

    private IDurableQueue GetQueue(IDurableQueueId id)
    {
        var rawId = id.ToRaw();
        return _orleans.GetGrain<IDurableQueue>(rawId);
    }

    private async Task ResubscribeLoop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            foreach (var listener in _listeners.Values)
            {
                try
                {
                    await listener.Resubscribe();
                    listener.Interval.RecordSuccess();
                }
                catch
                {
                    listener.Interval.RecordFailure();
                }
            }

            var delay = _listeners.IsEmpty
                ? TimeSpan.FromSeconds(10)
                : _listeners.Values.Min(l => l.Interval.GetNextDelay());

            await Task.Delay(delay, lifetime.Token);
        }
    }

    public class Listener
    {
        public required IDurableQueueId Id { get; init; }
        public required DurableQueueObserver ObserverSource { get; init; }
        public required IDurableQueueObserver ObserverReference { get; init; }
        public required IDurableQueue Queue { get; init; }
        public required ILogger Logger { get; init; }
        public required object Delegate { get; init; }
        public required IOrleans Orleans { get; init; }

        public AdaptiveInterval Interval { get; } = new(minInterval: TimeSpan.FromSeconds(10),
            maxInterval: TimeSpan.FromSeconds(60),
            failureBaseInterval: TimeSpan.FromSeconds(1));

        private int _consecutiveFailures;

        public async Task Resubscribe()
        {
            try
            {
                await Queue.AddObserver(ObserverSource.Id, ObserverReference);
                _consecutiveFailures = 0;
            }
            catch (Exception e)
            {
                _consecutiveFailures++;

                if (_consecutiveFailures == 1 || _consecutiveFailures % 10 == 0)
                    Logger.LogError(e,
                        "[Messaging] [DurableQueue] Failed to rebind observer (attempt {Count}) to queue {QueueId}",
                        _consecutiveFailures, Id.ToRaw());
            }
        }

        public void Cleanup()
        {
            Orleans.Client.DeleteObjectReference<IDurableQueueObserver>(ObserverReference);
            Queue.RemoveObserver(ObserverSource.Id).NoAwait();
        }
    }
}