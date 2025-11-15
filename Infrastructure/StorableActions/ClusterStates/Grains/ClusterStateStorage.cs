using Common;
using Infrastructure.Messaging;
using Interfaces;

namespace Grains;

public class ClusterStateStorage<T> : Grain, IClusterStateStorage<T>
{
    public ClusterStateStorage([States.ClusterState] IPersistentState<T> state, IMessaging messaging)
    {
        _state = state;
        _messaging = messaging;
    }

    private readonly IPersistentState<T> _state;
    private readonly IMessaging _messaging;

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