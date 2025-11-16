using Common;
using Infrastructure.Messaging;

namespace Infrastructure.StorableActions;

public class ClusterStateStorage<T> : Grain, IClusterStateStorage<T>
{
    public ClusterStateStorage([States.ClusterState] IPersistentState<T> state, IMessaging messaging)
    {
        _state = state;
        _messaging = messaging;
    }

    private readonly IMessaging _messaging;

    private readonly IPersistentState<T> _state;

    public Task Set(T value)
    {
        _state.State = value;
        return Task.WhenAll(
            _state.WriteStateAsync(),
            _messaging.PushDirectQueue(new ClusterStateMessageQueueId<T>(), value!)
        );
    }

    public ValueTask<T> Get()
    {
        return ValueTask.FromResult(_state.State);
    }
}