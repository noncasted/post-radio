using Cluster.Discovery;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cluster.Deploy;

public class DeployHealthChecker : BackgroundService
{
    public DeployHealthChecker(
        IServiceDiscovery discovery,
        IDeployContext deployContext,
        IMessaging messaging,
        IOrleans orleans,
        ILogger<DeployHealthChecker> logger)
    {
        _discovery = discovery;
        _deployContext = deployContext;
        _messaging = messaging;
        _orleans = orleans;
        _logger = logger;
    }

    private readonly IServiceDiscovery _discovery;
    private readonly IDeployContext _deployContext;
    private readonly IMessaging _messaging;
    private readonly IOrleans _orleans;
    private readonly ILogger<DeployHealthChecker> _logger;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan HeartbeatStaleThreshold = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken cancellation)
    {
        var lifetime = cancellation.ToLifetime();
        var serviceName = _discovery.Self.Tag.ToString();
        var isCoordinator = _discovery.Self.Tag == ServiceTag.Coordinator;

        while (_deployContext.DeployId == Guid.Empty && lifetime.IsTerminated == false)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellation);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        while (lifetime.IsTerminated == false)
        {
            try
            {
                await Task.Delay(PollInterval, cancellation);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await CheckDeployEpoch(lifetime, serviceName, isCoordinator);
            await CheckHeartbeat(serviceName);
        }
    }

    private async Task CheckDeployEpoch(IReadOnlyLifetime lifetime, string serviceName, bool isCoordinator)
    {
        try
        {
            var response = await _messaging.SendPipe<DeployIdResponse>(DeployIdPipe.Id, new DeployIdRequest());

            if (response.DeployId == Guid.Empty)
                return;

            if (response.DeployId == _deployContext.DeployId)
                return;

            if (isCoordinator)
            {
                _logger.LogWarning(
                    "[Health] Coordinator replaced: new deploy {New}, my deploy {Mine}, remaining passive",
                    response.DeployId, _deployContext.DeployId);
            }
            else
            {
                _logger.LogWarning("[Health] {Service} deploy epoch changed {Old} -> {New}, switching",
                    serviceName, _deployContext.DeployId, response.DeployId);

                await _deployContext.Set(response.DeployId, lifetime);
                await _discovery.Push();
            }
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "[Health] {Service} deploy id poll failed", serviceName);
        }
    }

    private async Task CheckHeartbeat(string serviceName)
    {
        try
        {
            var grain = _orleans.GetGrain<IDeployManagement>(_deployContext.DeployId);
            var state = await grain.GetState();
            var since = DateTime.UtcNow - state.LastHeartbeat;

            if (since > HeartbeatStaleThreshold)
            {
                _logger.LogWarning(
                    "[Health] {Service} coordinator unhealthy for deploy {DeployId}, last heartbeat {Seconds:F1}s ago",
                    serviceName, _deployContext.DeployId, since.TotalSeconds);
            }
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "[Health] {Service} heartbeat check failed", serviceName);
        }
    }
}