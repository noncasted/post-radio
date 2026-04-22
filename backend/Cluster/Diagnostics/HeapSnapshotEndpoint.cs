using Cluster.Coordination;
using Cluster.Discovery;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Cluster.Diagnostics;

public class HeapSnapshotEndpoint : ILocalSetupCompleted
{
    private readonly IMessaging _messaging;
    private readonly IServiceDiscovery _discovery;
    private readonly IHeapSnapshotCollector _collector;
    private readonly ILogger<HeapSnapshotEndpoint> _logger;

    public HeapSnapshotEndpoint(
        IMessaging messaging,
        IServiceDiscovery discovery,
        IHeapSnapshotCollector collector,
        ILogger<HeapSnapshotEndpoint> logger)
    {
        _messaging = messaging;
        _discovery = discovery;
        _collector = collector;
        _logger = logger;
    }

    public Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        _logger.LogInformation("[HeapSnapshot] Registering pipe handler on {Service}", _discovery.Self.Tag);

        _messaging.AddPipeRequestHandler<HeapSnapshotRequest, HeapSnapshotResponse>(
            lifetime,
            new MessagePipeServiceRequestId(_discovery.Self, typeof(HeapSnapshotRequest)),
            request =>
            {
                var response = _collector.Collect(
                    _discovery.Self.Tag.ToString(),
                    _discovery.Self.Id,
                    request.Deep);
                return Task.FromResult(response);
            });

        return Task.CompletedTask;
    }
}
