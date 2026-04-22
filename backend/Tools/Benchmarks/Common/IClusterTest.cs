using Common.Extensions;

namespace Benchmarks;

public interface IClusterTest
{
    string Group { get; }
    string Subgroup { get; }
    string Title { get; }
    string MetricName { get; }
    object Payload { get; set; }
    BenchmarkResult? LastResult { get; set; }
    Task Start(IOperationProgress progress, CancellationToken cancellationToken = default);
}