using Cluster.Deploy;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Infrastructure.Startup;

namespace Coordinator;

public class DeployIdentity : BackgroundService
{
    public DeployIdentity(
        IClusterParticipantContext participantContext,
        IDeployContext deployContext,
        IMessaging messaging,
        IOrleans orleans,
        ILogger<DeployIdentity> logger)
    {
        _participantContext = participantContext;
        _deployContext = deployContext;
        _messaging = messaging;
        _orleans = orleans;
        _logger = logger;
    }

    private readonly IClusterParticipantContext _participantContext;
    private readonly IDeployContext _deployContext;
    private readonly IMessaging _messaging;
    private readonly IOrleans _orleans;
    private readonly ILogger<DeployIdentity> _logger;

    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken cancellation)
    {
        var lifetime = cancellation.ToLifetime();

        try
        {
            await _participantContext.IsMessagingStarted.WaitTrue(lifetime);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var deployId = Guid.NewGuid();

        _logger.LogInformation("[DeployIdentity] Generated deploy id {DeployId}", deployId);

        var grain = _orleans.GetGrain<IDeployManagement>(deployId);
        await grain.Initialize();

        await _deployContext.Set(deployId, lifetime);

        await _messaging.AddPipeRequestHandler<DeployIdRequest, DeployIdResponse>(lifetime,
            DeployIdPipe.Id,
            _ => Task.FromResult(new DeployIdResponse { DeployId = deployId }));

        _logger.LogInformation("[DeployIdentity] Pipe handler bound, deploy id broadcast");

        HeartbeatLoop(lifetime, grain).NoAwait();
    }

    private async Task HeartbeatLoop(IReadOnlyLifetime lifetime, IDeployManagement grain)
    {
        while (lifetime.IsTerminated == false)
        {
            try
            {
                await grain.Heartbeat();
                await Task.Delay(HeartbeatInterval, lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[DeployIdentity] Heartbeat iteration failed");
                await Task.Delay(HeartbeatInterval);
            }
        }
    }
}