using Infrastructure.Messaging;

namespace Service;

public class MessageQueueObserver : IMessageQueueObserver
{
    public MessageQueueObserver(Action<object> onMessage)
    {
        _onMessage = onMessage;
    }

    private readonly Action<object> _onMessage;
    
    public Guid Id { get; } = Guid.NewGuid();

    public Task Send(IReadOnlyList<object> messages)
    {
        foreach (var message in messages)
            _onMessage(message);
        
        return Task.CompletedTask;
    }
}