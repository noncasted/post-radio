using Common;
using Infrastructure.Messaging;
using ServiceLoop;

namespace Infrastructure.Coordination;

public class ClusterCoordinator : ILocalSetupCompleted
{
    public ClusterCoordinator(
        IMessaging messaging,
        IClusterFeatures clusterFeatures,
        ILogger<ClusterCoordinator> logger)
    {
        _messaging = messaging;
        _clusterFeatures = clusterFeatures;
        _logger = logger;
    }

    private readonly IMessaging _messaging;
    private readonly IClusterFeatures _clusterFeatures;
    private readonly ILogger<ClusterCoordinator> _logger;

    public async Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        _logger.LogInformation("[Coordinator] Starting cluster coordinator...");
        
        await _clusterFeatures.SetAcceptingConnections(false);

        
        await _clusterFeatures.SetAcceptingConnections(true);
        
        _logger.LogInformation("[Coordinator] Cluster coordinator finished");

        await _messaging.PushDirectQueue(CoordinatorEvents.ReadyId, new CoordinatorEvents.ReadyPayload());
    }
}