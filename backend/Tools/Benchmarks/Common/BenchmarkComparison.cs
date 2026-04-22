namespace Benchmarks;

public enum MetricDirection
{
    HigherIsBetter,
    LowerIsBetter
}

public class BenchmarkComparison
{
    public static BenchmarkComparisonResult Compare(
        double currentMetric,
        double baselineMetric,
        MetricDirection direction,
        double threshold = BenchmarkOptions.RegressionThreshold)
    {
        if (baselineMetric <= 0)
        {
            return new BenchmarkComparisonResult
            {
                BaselineMetricValue = baselineMetric,
                RegressionPercent = 0,
                IsRegression = false
            };
        }

        var delta = (currentMetric - baselineMetric) / baselineMetric;

        var isRegression = direction == MetricDirection.HigherIsBetter
            ? currentMetric < baselineMetric * (1 - threshold)
            : currentMetric > baselineMetric * (1 + threshold);

        return new BenchmarkComparisonResult
        {
            BaselineMetricValue = baselineMetric,
            RegressionPercent = delta * 100,
            IsRegression = isRegression
        };
    }
}

public class BenchmarkComparisonResult
{
    public required double BaselineMetricValue { get; init; }
    public required double RegressionPercent { get; init; }
    public required bool IsRegression { get; init; }
}