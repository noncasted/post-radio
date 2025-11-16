using Common;
using Infrastructure.Loop;
using Infrastructure.Messaging;

namespace Frontend;

public interface IOnlineTracker
{
    void Track(IReadOnlyLifetime lifetime);
}

public class OnlineTracker : IOnlineTracker, ICoordinatorSetupCompleted
{
    public OnlineTracker(IMessaging messaging)
    {
        _messaging = messaging;
    }

    private readonly IMessaging _messaging;
    private readonly MessageQueueId _queueId = new("online-tracker");

    private int _count;

    public Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        Loop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    public void Track(IReadOnlyLifetime lifetime)
    {
        _count++;
        lifetime.Listen(() => _count--);
    }

    private async Task Loop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            await _messaging.PushDirectQueue(_queueId, new OnlineTrackerPayload
                {
                    Value = _count
                }
            );
            
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }
}