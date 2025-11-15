using Common;
using Infrastructure.Messaging;
using Infrastructure.Orleans;
using Microsoft.Extensions.Logging;

namespace Service;

public class MessageQueueClient : IMessageQueueClient
{
    public MessageQueueClient(IOrleans orleans, ILogger<MessageQueueClient> logger)
    {
        _orleans = orleans;
        _logger = logger;
    }

    private readonly IOrleans _orleans;
    private readonly ILogger<MessageQueueClient> _logger;
    private readonly Dictionary<Type, object> _delegates = new();
    private readonly List<Func<Task>> _resubscribeActions = new();
    
    private readonly Dictionary<IMessageQueueId, MessageQueueObserver> _observers = new();

    public Task Start(IReadOnlyLifetime lifetime)
    {
        ResubscribeLoop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    public IViewableDelegate<T> GetOrCreateConsumer<T>(IMessageQueueId id)
    {
        var type = typeof(T);

        if (_delegates.ContainsKey(type) == false)
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

            _delegates[type] = source;

            Task Subscribe()
            {
                try
                {
                    return GetQueue(id).AddObserver(observer.Id, observerReference);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "[Messaging] [Queue] Failed to rebind observer to queue {QueueId}",
                        id.ToRaw()
                    );
                    return Task.CompletedTask;
                }
            }
        }

        return (ViewableDelegate<T>)_delegates[type];
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