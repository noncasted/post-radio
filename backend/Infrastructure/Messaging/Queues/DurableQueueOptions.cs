using Common;

namespace Infrastructure;

[GrainState(Table = "configs", State = "message_queue_config", Lookup = "DurableQueueConfig",
    Key = GrainKeyType.String)]
public class DurableQueueOptions
{
    public int ObserverKeepAliveMinutes { get; set; } = 3;
}

public interface IDurableQueueConfig : IAddressableState<DurableQueueOptions>
{
}