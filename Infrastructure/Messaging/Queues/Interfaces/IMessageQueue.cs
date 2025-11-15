namespace Infrastructure.Messaging;

public interface IMessageQueue : IGrainWithStringKey
{
    Task AddObserver(Guid id, IMessageQueueObserver observer);
    Task PushDirect(object message);
    Task PushTransactional(object message);
}