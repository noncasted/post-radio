using Common;

namespace Infrastructure;

[GrainState(Table = "configs", State = "runtime_pipe_config", Lookup = "RuntimePipeConfig", Key = GrainKeyType.String)]
public class RuntimePipeOptions
{
    public int ObserverKeepAliveMinutes { get; set; } = 3;
    public int SendTimeoutSeconds { get; set; } = 10;
    public int SendRetryCount { get; set; } = 3;
    public int SendRetryBaseDelayMs { get; set; } = 500;
}

public interface IRuntimePipeConfig : IAddressableState<RuntimePipeOptions>
{
}