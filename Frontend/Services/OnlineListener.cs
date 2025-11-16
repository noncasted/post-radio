using Common;
using Infrastructure.Loop;
using Infrastructure.Messaging;

namespace Frontend;

public interface IOnlineListener : IViewableProperty<int>
{
}

public class OnlineListener : ViewableProperty<int>, IOnlineListener, ICoordinatorSetupCompleted
{
    public OnlineListener(IMessaging messaging) : base(0)
    {
        _messaging = messaging;
    }

    private readonly IMessaging _messaging;
    private readonly MessageQueueId _queueId = new("online-tracker");

    public Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        _messaging.ListenQueue<OnlineTrackerPayload>(lifetime, _queueId, payload => { Set(payload.Value); });

        return Task.CompletedTask;
    }
}