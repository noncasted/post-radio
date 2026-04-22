using Common.Reactive;
using Infrastructure.State;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IStateCollection<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
{
    IViewableDelegate Updated { get; }

    Task OnUpdated(TKey key, TValue value);
    Task OnUpdatedTransactional(TKey key, TValue value);
}

public class StateCollectionDurableQueueId<TKey, TValue> : IDurableQueueId
{
    public string ToRaw()
    {
        return $"state-collection-{typeof(TKey).FullName}-{typeof(TValue).FullName}";
    }
}

[GenerateSerializer]
public class StateCollectionUpdate<TKey, TValue>
{
    [Id(0)] public required TKey Key { get; init; }
    [Id(1)] public required TValue Value { get; init; }
    [Id(2)] public DateTime UpdatedAt { get; init; }
}

public class StateCollectionUtils<TKey, TValue>
    where TKey : notnull
    where TValue : class, IStateValue, new()
{
    public StateCollectionUtils(
        IGrainStatesRegistry statesRegistry,
        IStateStorage storage,
        IMessaging messaging,
        ILogger<StateCollectionUtils<TKey, TValue>> logger)
    {
        _statesRegistry = statesRegistry;
        _storage = storage;
        _messaging = messaging;
        _logger = logger;
    }

    private readonly IGrainStatesRegistry _statesRegistry;
    private readonly IStateStorage _storage;
    private readonly IMessaging _messaging;
    private readonly ILogger<StateCollectionUtils<TKey, TValue>> _logger;

    private readonly StateCollectionDurableQueueId<TKey, TValue> _queueId = new();

    public async Task<IReadOnlyDictionary<TKey, TValue>> Load(IReadOnlyLifetime lifetime)
    {
        var stateInfo = _statesRegistry.Get<TValue>();
        var grainStateType = stateInfo.Type;

        if (!typeof(TValue).IsAssignableFrom(grainStateType))
        {
            _logger.LogError("[StateCollectionUtils] Type mismatch: {GrainType} is not assignable to {Expected}",
                grainStateType, typeof(TValue));
            return new Dictionary<TKey, TValue>();
        }

        var reader = _storage.ReadAll<TKey, TValue>(lifetime);

        var dictionary = new Dictionary<TKey, TValue>();

        try
        {
            await foreach (var (key, value) in reader)
                dictionary.Add(key, value);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[StateCollectionUtils] Failed to load {Type}, loaded {Count} entries before failure",
                typeof(TValue).Name, dictionary.Count);
        }

        return dictionary;
    }

    public Task PushUpdate(TKey key, TValue value)
    {
        return _messaging.PushDirectQueue(_queueId, new StateCollectionUpdate<TKey, TValue>
        {
            Key = key,
            Value = value,
            UpdatedAt = DateTime.UtcNow
        });
    }

    public Task PushTransactionalUpdate(TKey key, TValue value)
    {
        _messaging.PushTransactionalQueue(_queueId, new StateCollectionUpdate<TKey, TValue>
        {
            Key = key,
            Value = value,
            UpdatedAt = DateTime.UtcNow
        });

        return Task.CompletedTask;
    }

    public Task ListenUpdates(IReadOnlyLifetime lifetime, Action<TKey, TValue, DateTime> onUpdate)
    {
        return _messaging.ListenDurableQueue<StateCollectionUpdate<TKey, TValue>>(lifetime,
            _queueId,
            update => onUpdate(update.Key, update.Value, update.UpdatedAt));
    }
}

public class StateCollection<TKey, TValue> :
    Dictionary<TKey, TValue>,
    IStateCollection<TKey, TValue>,
    ILocalSetupCompleted
    where TKey : notnull
    where TValue : class, IStateValue, new()
{
    public StateCollection(StateCollectionUtils<TKey, TValue> utils, ILogger<StateCollection<TKey, TValue>> logger)
    {
        _utils = utils;
        _logger = logger;
    }

    private readonly StateCollectionUtils<TKey, TValue> _utils;
    private readonly ILogger<StateCollection<TKey, TValue>> _logger;
    private readonly ViewableDelegate _updated = new();
    private readonly Dictionary<TKey, DateTime> _lastUpdated = new();

    public IViewableDelegate Updated => _updated;

    public async Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        try
        {
            var existing = await _utils.Load(lifetime);

            foreach (var (key, value) in existing)
                this[key] = value;

            await _utils.ListenUpdates(lifetime, (key, value, updatedAt) => {
                if (_lastUpdated.TryGetValue(key, out var last) && updatedAt <= last)
                    return;

                this[key] = value;
                _lastUpdated[key] = updatedAt;
                _updated.Invoke();
            });

            _logger.LogInformation("[StateCollection] Loaded {Count} entries for {Type}", Count, typeof(TValue).Name);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[StateCollection] Failed to initialize {Type}", typeof(TValue).Name);
        }
    }

    public Task OnUpdated(TKey key, TValue value)
    {
        this[key] = value;
        _lastUpdated[key] = DateTime.UtcNow;
        return _utils.PushUpdate(key, value);
    }

    public Task OnUpdatedTransactional(TKey key, TValue value)
    {
        return _utils.PushTransactionalUpdate(key, value);
    }
}