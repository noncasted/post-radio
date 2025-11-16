using Infrastructure.Messaging;

namespace Infrastructure.StorableActions;

public class ClusterStateMessageQueueId<T> : IMessageQueueId
{
    public string ToRaw()
    {
        var type = typeof(T);
        return $"cluster-state-{type.FullName!}";
    }
}