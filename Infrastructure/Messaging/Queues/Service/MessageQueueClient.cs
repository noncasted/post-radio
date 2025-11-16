using Common;
using Infrastructure.Orleans;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging;

public class MessageQueueClient : IMessageQueueClient
{
    public MessageQueueClient(IOrleans orleans, ILogger<MessageQueueClient> logger)
    {
        _orleans = orleans;
        _logger = logger;
    }

    private readonly Dictionary<string, object> _delegates = new();
    private readonly ILogger<MessageQueueClient> _logger;

    private readonly Dictionary<IMessageQueueId, MessageQueueObserver> _observers = new();

    private readonly IOrleans _orleans;
    private readonly List<Func<Task>> _resubscribeActions = new();

    public Task Start(IReadOnlyLifetime lifetime)
    {
        ResubscribeLoop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    public IViewableDelegate<T> GetOrCreateConsumer<T>(IMessageQueueId id)
    {
        var rawId = id.ToRaw();

        if (_delegates.ContainsKey(rawId) == false)
        {
            var source = new ViewableDelegate<T>();

            var observer = new MessageQueueObserver(message =>
                {
                    if (message is not T castedMessage)
                        throw new InvalidCastException();

                    source.Invoke(castedMessage);
                }
            );

            _observers[id] = observer;

            var observerReference = _orleans.Client.CreateObjectReference<IMessageQueueObserver>(observer);

            GC.KeepAlive(observer);
            GC.KeepAlive(observerReference);

            Subscribe().NoAwait();
            _resubscribeActions.Add(Subscribe);

            _delegates[rawId] = source;

            Task Subscribe()
            {
                try
                {
                    return GetQueue(id).AddObserver(observer.Id, observerReference);
                }
                catch (Exception e)
                {
                    _logger.LogError(
                        e,
                        "[Messaging] [Queue] Failed to rebind observer to queue {QueueId}",
                        rawId
                    );
                    return Task.CompletedTask;
                }
            }
        }

        return (ViewableDelegate<T>)_delegates[rawId];
    }

    public Task PushTransactional(IMessageQueueId id, object message)
    {
        return GetQueue(id).PushTransactional(message);
    }

    public Task PushDirect(IMessageQueueId id, object message)
    {
        return GetQueue(id).PushDirect(message);
    }

    private IMessageQueue GetQueue(IMessageQueueId id)
    {
        var rawId = id.ToRaw();
        return _orleans.GetGrain<IMessageQueue>(rawId);
    }

    private async Task ResubscribeLoop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            await Task.WhenAll(_resubscribeActions.Select(t => t()));
            await Task.Delay(TimeSpan.FromSeconds(10), lifetime.Token);
        }
    }
}