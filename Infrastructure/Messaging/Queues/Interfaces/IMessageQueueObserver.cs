namespace Infrastructure.Messaging;

public interface IMessageQueueObserver : IGrainObserver
{
    Task Send(IReadOnlyList<object> messages);
}