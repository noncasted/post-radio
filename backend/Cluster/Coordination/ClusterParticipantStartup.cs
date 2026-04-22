using Cluster.Deploy;
using Cluster.Discovery;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Infrastructure.Execution;
using Infrastructure.Startup;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cluster.Coordination;

public class ClusterParticipantStartup : BackgroundService
{
    public ClusterParticipantStartup(
        ITaskBalancer taskBalancer,
        IServiceDiscovery discovery,
        IServiceLoopObserver loopObserver,
        IServiceLoop loop,
        IMessaging messaging,
        IOrleans orleans,
        IDeployContext deployContext,
        IClusterParticipantContext context,
        ILogger<ClusterParticipantStartup> logger)
    {
        _taskBalancer = taskBalancer;
        _discovery = discovery;
        _loopObserver = loopObserver;
        _loop = loop;
        _messaging = messaging;
        _orleans = orleans;
        _deployContext = deployContext;
        _context = context;
        _logger = logger;
    }

    private readonly ITaskBalancer _taskBalancer;
    private readonly IServiceDiscovery _discovery;
    private readonly IServiceLoopObserver _loopObserver;
    private readonly IServiceLoop _loop;
    private readonly IMessaging _messaging;
    private readonly IOrleans _orleans;
    private readonly IDeployContext _deployContext;
    private readonly IClusterParticipantContext _context;
    private readonly ILogger<ClusterParticipantStartup> _logger;

    protected override async Task ExecuteAsync(CancellationToken cancellation)
    {
        var lifetime = cancellation.ToLifetime();
        var serviceName = _discovery.Self.Tag.ToString();
        var isCoordinator = _discovery.Self.Tag == ServiceTag.Coordinator;

        lifetime.Listen(() => _logger.LogError("[Startup] {Service} cancellation requested", serviceName));

        const string stageOrleans = "Orleans";
        const string stageTaskBalancer = "Task Balancer";
        const string stageMessaging = "Messaging";
        const string stageDeployId = "Deploy Id";
        const string stageDiscovery = "Service Discovery";
        const string stageWaitServices = "Waiting for Services";
        const string stageLocalSetup = "Local Setup";
        const string stageCoordinator = "Coordinator";

        _context.SetStages([
            stageOrleans,
            stageTaskBalancer,
            stageMessaging,
            stageDeployId,
            stageDiscovery,
            stageWaitServices,
            stageLocalSetup,
            stageCoordinator
        ]);

        _logger.LogInformation("[Startup] {Service} start", serviceName);

        _context.SetStage(stageOrleans);
        _logger.LogInformation("[Startup] {Service} waiting for orleans...", serviceName);

        await _loopObserver.IsOrleansStarted.WaitTrue(lifetime);

        _logger.LogInformation("[Startup] {Service} orleans started", serviceName);

        _context.SetStage(stageTaskBalancer);
        _logger.LogInformation("[Startup] {Service} starting task balancer", serviceName);

        await _loop.OnOrleansStarted(lifetime);
        await _taskBalancer.Run(lifetime);

        _logger.LogInformation("[Startup] {Service} task balancer started", serviceName);

        _context.SetStage(stageMessaging);
        _logger.LogInformation("[Startup] {Service} starting messaging", serviceName);

        await _messaging.Start(lifetime);
        _context.SetMessagingStarted();

        _logger.LogInformation("[Startup] {Service} messaging started", serviceName);

        _context.SetStage(stageDeployId);
        _logger.LogInformation("[Startup] {Service} waiting for deploy id via pipe...", serviceName);

        var deployId = await AcquireDeployId(lifetime);
        await _deployContext.Set(deployId, lifetime);

        _logger.LogInformation("[Startup] {Service} got deploy id {DeployId}", serviceName, deployId);

        _context.SetStage(stageDiscovery);
        _logger.LogInformation("[Startup] {Service} starting service discovery", serviceName);

        await _discovery.Start(lifetime);
        await _discovery.Push();

        _logger.LogInformation("[Startup] {Service} service discovery started", serviceName);

        _context.SetStage(stageWaitServices);
        _logger.LogInformation("[Startup] {Service} waiting for cluster to be ready...", serviceName);

        await WaitClusterReady();

        _logger.LogInformation("[Startup] {Service} cluster is ready", serviceName);

        _context.SetStage(stageLocalSetup);
        _logger.LogInformation("[Startup] {Service} running local setup loop", serviceName);

        await _loop.OnLocalSetupCompleted(lifetime);

        _logger.LogInformation("[Startup] {Service} local setup loop completed", serviceName);

        _context.SetStage(stageCoordinator);

        if (isCoordinator == false)
        {
            _logger.LogInformation("[Startup] {Service} waiting for coordinator to be ready", serviceName);
            await WaitCoordinatorReady();
        }

        await _loop.OnCoordinatorSetupCompleted(lifetime);

        _logger.LogInformation("[Startup] {Service} coordinator is ready", serviceName);
        _logger.LogInformation("[Startup] {Service} startup finished", serviceName);

        _context.Initialize();

        _logger.LogInformation("[Startup] {Service} cluster participant initialized", serviceName);

        _loop.OnServiceStarted(lifetime).NoAwait();

        return;

        async Task<Guid> AcquireDeployId(IReadOnlyLifetime lf)
        {
            var missingSince = (DateTime?)null;
            var warnThreshold = TimeSpan.FromSeconds(5);

            while (lf.IsTerminated == false)
            {
                try
                {
                    if (await _messaging.IsPipeExists(DeployIdPipe.Id) == false)
                    {
                        missingSince ??= DateTime.UtcNow;

                        if (DateTime.UtcNow - missingSince.Value >= warnThreshold)
                            _logger.LogWarning(
                                "[Startup] {Service} deploy id pipe observer is missing for {Elapsed}s, still waiting",
                                serviceName, (DateTime.UtcNow - missingSince.Value).TotalSeconds);

                        await Task.Delay(TimeSpan.FromSeconds(0.2), cancellation);
                        continue;
                    }

                    missingSince = null;

                    var response = await _messaging.SendPipe<DeployIdResponse>(DeployIdPipe.Id, new DeployIdRequest());

                    if (response.DeployId != Guid.Empty)
                        return response.DeployId;
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "[Startup] {Service} failed to acquire deploy id, retrying",
                        serviceName);
                }

                await Task.Delay(TimeSpan.FromSeconds(0.2), cancellation);
            }

            throw new OperationCanceledException("Deploy id acquisition cancelled");
        }

        async Task WaitClusterReady()
        {
            while (lifetime.IsTerminated == false)
            {
                try
                {
                    await _discovery.Push();

                    var present = _discovery.Entries.Values.Select(m => m.Tag).ToHashSet();

                    var missing = DeployConstants.RequiredServices
                                                 .Where(tag => present.Contains(tag) == false)
                                                 .Select(tag => tag.ToString())
                                                 .ToList();

                    if (missing.Count == 0)
                        return;

                    _logger.LogWarning("[Startup] {Service} cluster not ready, missing: {Missing}",
                        serviceName, string.Join(", ", missing));
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "[Startup] {Service} cluster readiness poll failed", serviceName);
                }

                await Task.Delay(TimeSpan.FromSeconds(0.2), cancellation);
            }
        }

        async Task WaitCoordinatorReady()
        {
            var grain = _orleans.GetGrain<IDeployManagement>(_deployContext.DeployId);
            var pollCount = 0;

            while (lifetime.IsTerminated == false)
            {
                try
                {
                    var state = await grain.GetState();

                    if (state.CoordinatorReady)
                        return;

                    pollCount++;

                    if (pollCount % 25 == 1)
                        _logger.LogInformation(
                            "[Startup] {Service} still waiting on CoordinatorReady (poll {Count}, deploy {DeployId})",
                            serviceName, pollCount, _deployContext.DeployId);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "[Startup] {Service} coordinator readiness poll failed", serviceName);
                }

                await Task.Delay(TimeSpan.FromSeconds(0.2), cancellation);
            }
        }
    }
}