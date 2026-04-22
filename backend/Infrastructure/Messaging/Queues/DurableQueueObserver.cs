namespace Infrastructure;

public interface IDurableQueueObserver : IGrainObserver
{
    Task Send(object message);
}

public class DurableQueueObserver : IDurableQueueObserver
{
    public DurableQueueObserver(Action<object> onMessage)
    {
        _onMessage = onMessage;
    }

    private readonly Action<object> _onMessage;

    public Guid Id { get; } = Guid.NewGuid();

    public Task Send(object message)
    {
        _onMessage(message);
        return Task.CompletedTask;
    }
}