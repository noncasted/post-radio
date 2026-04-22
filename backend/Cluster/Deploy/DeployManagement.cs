using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;

namespace Cluster.Deploy;

public interface IDeployManagement : IGrainWithGuidKey
{
    Task Initialize();

    Task Heartbeat();

    Task MarkCoordinatorReady();

    Task<DeployState> GetState();
}

[Reentrant]
public class DeployManagement : Grain, IDeployManagement
{
    public DeployManagement(
        [State] State<DeployState> state,
        ILogger<DeployManagement> logger)
    {
        _state = state;
        _logger = logger;
    }

    private readonly State<DeployState> _state;
    private readonly ILogger<DeployManagement> _logger;

    public async Task Initialize()
    {
        var deployId = this.GetPrimaryKey();

        await _state.Write(s => {
            s.DeployId = deployId;
            s.LastHeartbeat = DateTime.UtcNow;
            s.CoordinatorReady = false;
        });

        _logger.LogInformation("[DeployManagement] Initialized deploy {DeployId}", deployId);
    }

    public async Task Heartbeat()
    {
        await _state.Write(s => s.LastHeartbeat = DateTime.UtcNow);
    }

    public async Task MarkCoordinatorReady()
    {
        await _state.Write(s => {
            s.CoordinatorReady = true;
            s.LastHeartbeat = DateTime.UtcNow;
        });

        _logger.LogInformation("[DeployManagement] Coordinator marked ready for deploy {DeployId}",
            this.GetPrimaryKey());
    }

    public Task<DeployState> GetState()
    {
        return _state.ReadValue();
    }
}