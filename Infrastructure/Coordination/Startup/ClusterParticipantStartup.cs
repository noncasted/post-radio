using Common;
using Infrastructure.Discovery;
using Infrastructure.Loop;
using Infrastructure.Messaging;
using Infrastructure.TaskScheduling;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Coordination;

public class ClusterParticipantStartup : BackgroundService
{
    public ClusterParticipantStartup(
        ITaskBalancer taskBalancer,
        IServiceDiscovery discovery,
        IServiceLoopObserver loopObserver,
        IServiceLoop loop,
        IMessaging messaging,
        ILogger<ClusterParticipantStartup> logger)
    {
        _taskBalancer = taskBalancer;
        _discovery = discovery;
        _loopObserver = loopObserver;
        _loop = loop;
        _messaging = messaging;
        _logger = logger;
    }

    private readonly ITaskBalancer _taskBalancer;
    private readonly IServiceDiscovery _discovery;
    private readonly IServiceLoopObserver _loopObserver;
    private readonly IServiceLoop _loop;
    private readonly IMessaging _messaging;
    private readonly ILogger<ClusterParticipantStartup> _logger;

    protected override async Task ExecuteAsync(CancellationToken cancellation)
    {
        var lifetime = cancellation.ToLifetime();
        var startupLifetime = lifetime.Child();
        var serviceName = _discovery.Self.Tag.ToString();
        var coordinatorCompletion = new TaskCompletionSource();

        lifetime.Listen(() => _logger.LogError("[Startup] {Service} cancellation requested", serviceName));

        _logger.LogInformation("[Startup] {Service} start", serviceName);
        _logger.LogInformation("[Startup] {Service} waiting for orleans...", serviceName);

        await _loopObserver.IsOrleansStarted.WaitTrue(lifetime);

        _logger.LogInformation("[Startup] {Service} orleans started", serviceName);
        _logger.LogInformation("[Startup] {Service} starting task balancer", serviceName);

        await _taskBalancer.Run(lifetime);

        _logger.LogInformation("[Startup] {Service} task balancer started", serviceName);
        _logger.LogInformation("[Startup] {Service} starting messaging", serviceName);

        await _messaging.Start(lifetime);

        _messaging.ListenQueue<CoordinatorEvents.ReadyPayload>(
            startupLifetime,
            CoordinatorEvents.ReadyId,
            _ => coordinatorCompletion.TrySetResult()
        );

        _logger.LogInformation("[Startup] {Service} messaging started", serviceName);
        _logger.LogInformation("[Startup] {Service} starting service discovery", serviceName);

        await _discovery.Start(lifetime);

        _logger.LogInformation("[Startup] {Service} service discovery started", serviceName);
        _logger.LogInformation("[Startup] {Service} waiting for other services...", serviceName);

        await WaitDiscovery();

        _logger.LogInformation("[Startup] {Service} all required services found", serviceName);
        _logger.LogInformation("[Startup] {Service} running local setup loop", serviceName);

        await _loop.OnLocalSetupCompleted(lifetime);

        _logger.LogInformation("[Startup] {Service} local setup loop completed", serviceName);
        _logger.LogInformation("[Startup] {Service} waiting for coordinator to be ready", serviceName);

        await coordinatorCompletion.Task;
        await _loop.OnCoordinatorSetupCompleted(lifetime);

        _logger.LogInformation("[Startup] {Service} coordinator is ready", serviceName);
        _logger.LogInformation("[Startup] {Service} startup finished", serviceName);

        startupLifetime.Terminate();

        return;

        async Task WaitDiscovery()
        {
            var requiredServices = new[]
            {
                ServiceTag.Coordinator,
                ServiceTag.Console,
                ServiceTag.Frontend,
                ServiceTag.Silo,
            };

            while (lifetime.IsTerminated == false && AllServicesFound() == false)
                await Task.Delay(TimeSpan.FromSeconds(1), cancellation);

            return;

            bool AllServicesFound()
            {
                var foundServices = _discovery.Entries.Values
                    .Select(entry => entry.Tag)
                    .Distinct()
                    .ToHashSet();

                var servicesToAwait = requiredServices
                    .Where(tag => foundServices.Contains(tag) == false)
                    .Select(tag => tag.ToString())
                    .ToList();

                if (servicesToAwait.Count == 0)
                    return true;

                _logger.LogWarning("[Startup] {Service} waiting for services: {RequiredServices}",
                    serviceName,
                    string.Join(", ", servicesToAwait)
                );

                return false;
            }
        }
    }
}