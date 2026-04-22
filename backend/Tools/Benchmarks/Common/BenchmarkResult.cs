namespace Benchmarks;

public class BenchmarkResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BenchmarkName { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public double MetricValue { get; set; }
    public long DurationMs { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}