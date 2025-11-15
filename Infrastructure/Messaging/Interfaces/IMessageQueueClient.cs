using Common;

namespace Infrastructure.Messaging;

public interface IMessageQueueClient
{
    Task Start(IReadOnlyLifetime lifetime);
    
    IViewableDelegate<T> GetOrCreateConsumer<T>(IMessageQueueId id);
    Task PushTransactional(IMessageQueueId id, object message);
    Task PushDirect(IMessageQueueId id, object message);
}