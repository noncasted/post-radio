namespace Infrastructure;

[GenerateSerializer]
public class HeapSnapshotRequest
{
    [Id(0)] public bool Deep { get; set; }
}

[GenerateSerializer]
public class HeapSnapshotResponse
{
    [Id(0)] public string ServiceName { get; set; } = "";
    [Id(1)] public Guid ServiceId { get; set; }
    [Id(2)] public DateTime Timestamp { get; set; }
    [Id(3)] public long WorkingSetBytes { get; set; }
    [Id(4)] public long GcTotalBytes { get; set; }
    [Id(5)] public long Gen0SizeBytes { get; set; }
    [Id(6)] public long Gen1SizeBytes { get; set; }
    [Id(7)] public long Gen2SizeBytes { get; set; }
    [Id(8)] public long LohSizeBytes { get; set; }
    [Id(9)] public long PohSizeBytes { get; set; }
    [Id(10)] public IReadOnlyList<HeapTypeEntry> TopTypes { get; set; } = Array.Empty<HeapTypeEntry>();
    [Id(11)] public string? Error { get; set; }
    [Id(12)] public long CollectDurationMs { get; set; }
    [Id(13)] public bool Deep { get; set; }
}

[GenerateSerializer]
public class HeapTypeEntry
{
    [Id(0)] public string TypeName { get; set; } = "";
    [Id(1)] public long Count { get; set; }
    [Id(2)] public long TotalBytes { get; set; }
}
