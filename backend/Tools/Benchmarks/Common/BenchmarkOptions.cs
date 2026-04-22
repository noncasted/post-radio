namespace Benchmarks;

public static class BenchmarkOptions
{
    public const int MetricStep = 50;
    public const int Samples = 50;
    public static readonly TimeSpan CollectStep = TimeSpan.FromSeconds(0.02f);
    public const double RegressionThreshold = 0.10;
}