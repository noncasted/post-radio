using Common;
using Infrastructure.State;

namespace Benchmarks;

public class BenchmarkMetricsHandle
{
    private readonly List<BenchmarkRecord> _records = new();
    private readonly Lock _lock = new();

    private int _count;
    private DateTime _startTime;
    private DateTime _snapshotTime;

    public void Inc()
    {
        lock (_lock)
        {
            if (_count == 0)
            {
                _snapshotTime = DateTime.UtcNow;

                if (_records.Count == 0)
                    _startTime = _snapshotTime;
            }

            _count++;

            if (DateTime.UtcNow - _snapshotTime < BenchmarkOptions.CollectStep)
                return;

            var record = new BenchmarkRecord
            {
                Count = _count,
                Time = DateTime.UtcNow - _startTime
            };

            _count = 0;
            _records.Add(record);
        }
    }

    public BenchmarkState Collect()
    {
        lock (_lock)
        {
            if (_count > 0)
            {
                _records.Add(new BenchmarkRecord
                {
                    Count = _count,
                    Time = DateTime.UtcNow - _startTime
                });
                _count = 0;
            }

            var aggregated = new List<BenchmarkRecord>();
            var step = Math.Max(1, _records.Count / BenchmarkOptions.Samples);

            for (var i = 0; i < _records.Count; i += step)
            {
                var isLastChunk = i + step >= _records.Count;
                var isIncomplete = _records.Count - i < step;

                // Merge incomplete last chunk into previous
                if (isLastChunk && isIncomplete && aggregated.Count > 0)
                {
                    var prev = aggregated[^1];
                    var totalCount = prev.Count;
                    var previousTime = i > 0 ? _records[i - 1].Time : TimeSpan.Zero;
                    var prevDuration = prev.Time;

                    for (var j = i; j < _records.Count; j++)
                        totalCount += _records[j].Count;

                    var extraDuration = _records[^1].Time - previousTime;

                    aggregated[^1] = new BenchmarkRecord
                    {
                        Count = totalCount,
                        Time = prevDuration + extraDuration
                    };
                    break;
                }

                var end = Math.Min(i + step, _records.Count);
                var count = 0;
                var prevTime = i > 0 ? _records[i - 1].Time : TimeSpan.Zero;

                for (var j = i; j < end; j++)
                    count += _records[j].Count;

                var duration = _records[end - 1].Time - prevTime;

                aggregated.Add(new BenchmarkRecord
                {
                    Count = count,
                    Time = duration
                });
            }

            var totalDuration = _records.Count > 0 ? _records[^1].Time : TimeSpan.Zero;

            return new BenchmarkState
            {
                Id = Guid.NewGuid(),
                Date = _startTime,
                Duration = totalDuration,
                Records = aggregated
            };
        }
    }
}

[GenerateSerializer]
[GrainState(Table = "state_benchmark", State = "benchmark", Lookup = "Benchmark", Key = GrainKeyType.Guid)]
public class BenchmarkState : IStateValue
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public DateTime Date { get; set; }
    [Id(3)] public TimeSpan Duration { get; set; }
    [Id(4)] public List<BenchmarkRecord> Records { get; set; } = [];
    [Id(5)] public bool Success { get; set; }
    [Id(6)] public bool IsBaseline { get; set; }
    [Id(7)] public double BaselineMetricValue { get; set; }
    [Id(8)] public double RegressionPercent { get; set; }
    [Id(9)] public bool IsRegression { get; set; }
    public int Version => 0;

    public double CalculateMetricValue() =>
        Duration.TotalSeconds > 0 ? Records.Sum(r => r.Count) / Duration.TotalSeconds : 0;
}

[GenerateSerializer]
public class BenchmarkRecord
{
    [Id(0)] public required int Count { get; init; }
    [Id(1)] public required TimeSpan Time { get; init; }
}