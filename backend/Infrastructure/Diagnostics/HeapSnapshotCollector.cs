using Microsoft.Diagnostics.Runtime;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IHeapSnapshotCollector
{
    HeapSnapshotResponse Collect(string serviceName, Guid serviceId, bool deep, int topN = 150);
}

public class HeapSnapshotCollector : IHeapSnapshotCollector
{
    private static readonly SemaphoreSlim CollectLock = new(1, 1);

    private readonly ILogger<HeapSnapshotCollector> _logger;

    public HeapSnapshotCollector(ILogger<HeapSnapshotCollector> logger)
    {
        _logger = logger;
    }

    public HeapSnapshotResponse Collect(string serviceName, Guid serviceId, bool deep, int topN = 150)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = new HeapSnapshotResponse
        {
            ServiceName = serviceName,
            ServiceId = serviceId,
            Timestamp = DateTime.UtcNow,
            WorkingSetBytes = Environment.WorkingSet,
            Deep = deep,
        };

        if (!CollectLock.Wait(TimeSpan.FromSeconds(5)))
        {
            response.Error = "[HeapSnapshot] Another collection is in progress";
            sw.Stop();
            response.CollectDurationMs = sw.ElapsedMilliseconds;
            return response;
        }

        try
        {
            var info = GC.GetGCMemoryInfo();
            response.GcTotalBytes = GC.GetTotalMemory(false);

            var gens = info.GenerationInfo;
            if (gens.Length > 0) response.Gen0SizeBytes = gens[0].SizeAfterBytes;
            if (gens.Length > 1) response.Gen1SizeBytes = gens[1].SizeAfterBytes;
            if (gens.Length > 2) response.Gen2SizeBytes = gens[2].SizeAfterBytes;
            if (gens.Length > 3) response.LohSizeBytes = gens[3].SizeAfterBytes;
            if (gens.Length > 4) response.PohSizeBytes = gens[4].SizeAfterBytes;

            if (deep)
            {
                _logger.LogWarning("[HeapSnapshot] Deep collection requested on {ServiceName} — fork may stall Orleans silo",
                    serviceName);
                response.TopTypes = CollectTopTypes(topN, response);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[HeapSnapshot] Collection failed for {ServiceName}", serviceName);
            response.Error = e.ToString();
        }
        finally
        {
            CollectLock.Release();
        }

        sw.Stop();
        response.CollectDurationMs = sw.ElapsedMilliseconds;
        return response;
    }

    private List<HeapTypeEntry> CollectTopTypes(int topN, HeapSnapshotResponse response)
    {
        // WARNING: CreateSnapshotAndAttach forks the process on Linux. The fork
        // inherits all file descriptors (PostgreSQL, Orleans sockets, Npgsql pool).
        // When the child exits, its FDs close and send FIN/RST to every peer, which
        // breaks the live silo's connection pool. In addition, copying page tables
        // for a large process can stall managed threads long enough to trip the
        // Orleans stall detector and kill the silo.
        //
        // Only use when a service restart is acceptable immediately after.

        DataTarget? target = null;

        try
        {
            target = DataTarget.CreateSnapshotAndAttach(Environment.ProcessId);

            if (target.ClrVersions.Length == 0)
            {
                response.Error = "[HeapSnapshot] No CLR versions found in target process";
                return [];
            }

            using var runtime = target.ClrVersions[0].CreateRuntime();
            var heap = runtime.Heap;

            if (!heap.CanWalkHeap)
            {
                response.Error = "[HeapSnapshot] Heap walk not available in snapshot";
                return [];
            }

            var stats = new Dictionary<string, (long Count, long Bytes)>(capacity: 4096);
            var corruptedCount = 0L;

            foreach (var obj in heap.EnumerateObjects())
            {
                string typeName;
                long size;

                try
                {
                    if (obj.IsFree) continue;

                    typeName = obj.Type?.Name ?? "?";
                    size = (long)obj.Size;
                }
                catch
                {
                    corruptedCount++;
                    continue;
                }

                if (stats.TryGetValue(typeName, out var existing))
                    stats[typeName] = (existing.Count + 1, existing.Bytes + size);
                else
                    stats[typeName] = (1, size);
            }

            if (corruptedCount > 0)
                _logger.LogInformation("[HeapSnapshot] Skipped {Count} corrupted objects during walk",
                    corruptedCount);

            return stats
                .OrderByDescending(kv => kv.Value.Bytes)
                .Take(topN)
                .Select(kv => new HeapTypeEntry
                {
                    TypeName = kv.Key,
                    Count = kv.Value.Count,
                    TotalBytes = kv.Value.Bytes,
                })
                .ToList();
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "[HeapSnapshot] Deep heap walk failed, returning GC stats only");
            response.Error = $"[HeapSnapshot] Deep heap walk unavailable: {e.Message}. GC stats collected.";
            return [];
        }
        finally
        {
            target?.Dispose();
        }
    }
}
