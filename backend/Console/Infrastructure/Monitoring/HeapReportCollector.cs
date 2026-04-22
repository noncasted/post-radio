using Cluster.Coordination;
using Cluster.Discovery;
using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Console.Infrastructure.Monitoring;

public class HeapReportCollector
{
    private readonly IMessaging _messaging;
    private readonly IServiceDiscovery _discovery;
    private readonly IHeapReportStorage _storage;
    private readonly ILogger<HeapReportCollector> _logger;

    public HeapReportCollector(
        IMessaging messaging,
        IServiceDiscovery discovery,
        IHeapReportStorage storage,
        ILogger<HeapReportCollector> logger)
    {
        _messaging = messaging;
        _discovery = discovery;
        _storage = storage;
        _logger = logger;
    }

    public async Task<HeapReport> CollectAll(bool deep)
    {
        var services = _discovery.Entries.Values.ToList();

        _logger.LogInformation("[HeapReport] Collecting {Mode} snapshots from {Count} services",
            deep ? "deep" : "quick", services.Count);

        var tasks = services.Select(async svc => {
            try
            {
                var pipeId = new MessagePipeServiceRequestId(svc, typeof(HeapSnapshotRequest));
                return await _messaging.SendPipe<HeapSnapshotResponse>(pipeId,
                    new HeapSnapshotRequest { Deep = deep });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[HeapReport] Failed to collect snapshot from {Service}", svc.Tag);
                return new HeapSnapshotResponse
                {
                    ServiceName = svc.Tag.ToString(),
                    ServiceId = svc.Id,
                    Timestamp = DateTime.UtcNow,
                    Deep = deep,
                    Error = e.ToString(),
                };
            }
        }).ToList();

        var snapshots = await Task.WhenAll(tasks);
        var report = _storage.Save(snapshots, DateTime.UtcNow, deep);

        _logger.LogInformation("[HeapReport] Collection complete, wrote {FileName}", report.FileName);

        return report;
    }
}
