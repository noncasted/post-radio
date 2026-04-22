using Common;
using Infrastructure;

namespace Cluster.Configs;

[GrainState(Table = "configs", State = "frontend_config", Lookup = "FrontendConfig",
    Key = GrainKeyType.String)]
public class FrontendOptions
{
    public float BaseVolume { get; set; } = 0.25f;
    public float MaxVolume { get; set; } = 0.5f;
    public int ImageSwitchIntervalMs { get; set; } = 8000;
    public int ImageFadeMs { get; set; } = 1000;
}

public interface IFrontendConfig : IAddressableState<FrontendOptions>
{
}
