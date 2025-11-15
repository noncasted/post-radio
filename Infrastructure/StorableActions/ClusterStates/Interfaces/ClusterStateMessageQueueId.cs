using Infrastructure.Messaging;

namespace Interfaces;

public class ClusterStateMessageQueueId<T> : IMessageQueueId
{
    public string ToRaw()
    {
        var type = typeof(T);
        return $"cluster-state-{type.FullName!}";
    }
}