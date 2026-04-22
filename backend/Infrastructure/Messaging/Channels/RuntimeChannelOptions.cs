using Common;

namespace Infrastructure;

[GrainState(Table = "configs", State = "runtime_channel_config", Lookup = "RuntimeChannelConfig",
    Key = GrainKeyType.String)]
public class RuntimeChannelOptions
{
    public int ObserverKeepAliveMinutes { get; set; } = 3;
    public int CatchUpBufferSize { get; set; } = 1024;
    public int DeliveryTimeoutSeconds { get; set; } = 5;
}

public interface IRuntimeChannelConfig : IAddressableState<RuntimeChannelOptions>
{
}