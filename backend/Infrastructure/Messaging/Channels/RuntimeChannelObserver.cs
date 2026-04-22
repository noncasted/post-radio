namespace Infrastructure;

public interface IRuntimeChannelObserver : IGrainObserver
{
    Task Send(object message);
}

public class RuntimeChannelObserver : IRuntimeChannelObserver
{
    public RuntimeChannelObserver(Action<object> onMessage)
    {
        _onMessage = onMessage;
    }

    private readonly Action<object> _onMessage;

    public Guid Id { get; } = Guid.NewGuid();
    public long LastSeenSequence { get; private set; }

    public Task Send(object message)
    {
        if (message is SequencedMessage sequenced)
        {
            if (sequenced.Sequence > LastSeenSequence)
                LastSeenSequence = sequenced.Sequence;

            _onMessage(sequenced.Payload);
        }
        else
        {
            _onMessage(message);
        }

        return Task.CompletedTask;
    }
}