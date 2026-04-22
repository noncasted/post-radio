using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Console.Infrastructure.Monitoring;

public class HeapReport
{
    public required string FileName { get; init; }
    public required string FilePath { get; init; }
    public required DateTime Timestamp { get; init; }
    public required long SizeBytes { get; init; }
}

public interface IHeapReportStorage
{
    string Directory { get; }
    IReadOnlyList<HeapReport> List();
    HeapReport Save(IReadOnlyList<HeapSnapshotResponse> snapshots, DateTime timestamp, bool deep);
    byte[] ReadBytes(string fileName);
    bool TryGet(string fileName, out HeapReport? report);
}

public class HeapReportStorage : IHeapReportStorage
{
    private const string FilePrefix = "heap-report-";
    private const string FileSuffix = ".txt";

    private readonly string _directory;
    private readonly ILogger<HeapReportStorage> _logger;
    private readonly object _lock = new();

    public HeapReportStorage(ILogger<HeapReportStorage> logger)
    {
        _logger = logger;
        _directory = ResolveDirectory();
        System.IO.Directory.CreateDirectory(_directory);
        _logger.LogInformation("[HeapReport] Storage directory: {Directory}", _directory);
    }

    public string Directory => _directory;

    public IReadOnlyList<HeapReport> List()
    {
        lock (_lock)
        {
            if (!System.IO.Directory.Exists(_directory))
                return [];

            return System.IO.Directory
                          .EnumerateFiles(_directory, $"{FilePrefix}*{FileSuffix}")
                          .Select(path => {
                              var info = new FileInfo(path);
                              return new HeapReport
                              {
                                  FileName = info.Name,
                                  FilePath = info.FullName,
                                  Timestamp = info.LastWriteTimeUtc,
                                  SizeBytes = info.Length,
                              };
                          })
                          .OrderByDescending(r => r.Timestamp)
                          .ToList();
        }
    }

    public HeapReport Save(IReadOnlyList<HeapSnapshotResponse> snapshots, DateTime timestamp, bool deep)
    {
        var mode = deep ? "deep" : "quick";
        var fileName = $"{FilePrefix}{mode}-{timestamp:yyyyMMdd-HHmmss}{FileSuffix}";
        var filePath = Path.Combine(_directory, fileName);
        var text = HeapReportFormatter.FormatReport(timestamp, snapshots);

        lock (_lock)
        {
            File.WriteAllText(filePath, text);
        }

        var info = new FileInfo(filePath);
        _logger.LogInformation("[HeapReport] Saved report {FileName} ({Size} bytes)", fileName, info.Length);

        return new HeapReport
        {
            FileName = info.Name,
            FilePath = info.FullName,
            Timestamp = info.LastWriteTimeUtc,
            SizeBytes = info.Length,
        };
    }

    public byte[] ReadBytes(string fileName)
    {
        var filePath = ResolveSafePath(fileName);

        lock (_lock)
        {
            return File.ReadAllBytes(filePath);
        }
    }

    public bool TryGet(string fileName, out HeapReport? report)
    {
        report = null;

        string filePath;
        try
        {
            filePath = ResolveSafePath(fileName);
        }
        catch
        {
            return false;
        }

        if (!File.Exists(filePath))
            return false;

        var info = new FileInfo(filePath);
        report = new HeapReport
        {
            FileName = info.Name,
            FilePath = info.FullName,
            Timestamp = info.LastWriteTimeUtc,
            SizeBytes = info.Length,
        };
        return true;
    }

    private string ResolveSafePath(string fileName)
    {
        if (fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
            throw new ArgumentException($"Invalid file name: {fileName}");

        if (!fileName.StartsWith(FilePrefix) || !fileName.EndsWith(FileSuffix))
            throw new ArgumentException($"Invalid file name: {fileName}");

        return Path.Combine(_directory, fileName);
    }

    private static string ResolveDirectory()
    {
        var fromEnv = Environment.GetEnvironmentVariable("HEAP_REPORTS_DIR");

        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv;

        var fromTelemetry = TelemetryPaths.GetTelemetryDir("heap-reports");

        if (fromTelemetry != null)
            return fromTelemetry;

        return Path.Combine(AppContext.BaseDirectory, "heap-reports");
    }
}
