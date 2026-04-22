using System.Threading.Channels;
using Common.Extensions;
using Common.Reactive;
using Microsoft.Extensions.Logging;

namespace Benchmarks;

public class BenchmarkRunInfo
{
    public required IClusterTest Test { get; init; }
    public required IOperationProgress Progress { get; init; }
    public required CancellationTokenSource Cts { get; init; }
    public required DateTime StartedAt { get; init; }
}

public class BenchmarkRunner
{
    public BenchmarkRunner(ILogger<BenchmarkRunner> logger)
    {
        _logger = logger;
        _ = ProcessQueue();
    }

    private readonly ILogger<BenchmarkRunner> _logger;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, BenchmarkRunInfo> _all = new();
    private readonly Channel<BenchmarkRunInfo> _queue = Channel.CreateUnbounded<BenchmarkRunInfo>();
    private readonly EventSource<string> _completed = new();

    public IEventSource<string> Completed => _completed;

    public IOperationProgress Start(IClusterTest test)
    {
        lock (_lock)
        {
            if (_all.TryGetValue(test.Title, out var existing))
                return existing.Progress;
        }

        var progress = new OperationProgress();
        var cts = new CancellationTokenSource();

        var info = new BenchmarkRunInfo
        {
            Test = test,
            Progress = progress,
            Cts = cts,
            StartedAt = DateTime.UtcNow
        };

        lock (_lock)
        {
            _all[test.Title] = info;
        }

        _queue.Writer.TryWrite(info);
        return progress;
    }

    public bool Cancel(string title)
    {
        BenchmarkRunInfo? info;

        lock (_lock)
        {
            if (!_all.TryGetValue(title, out info))
                return false;
            _all.Remove(title);
        }

        info.Cts.Cancel();
        info.Progress.SetStatus(OperationStatus.Cancelled);
        return true;
    }

    public void CancelAll()
    {
        List<BenchmarkRunInfo> all;

        lock (_lock)
        {
            all = _all.Values.ToList();
            _all.Clear();
        }

        foreach (var info in all)
        {
            info.Cts.Cancel();
            info.Progress.SetStatus(OperationStatus.Cancelled);
        }
    }

    public bool HasQueued()
    {
        lock (_lock)
        {
            return _all.Count > 0;
        }
    }

    public BenchmarkRunInfo? GetRunInfo(string title)
    {
        lock (_lock)
        {
            return _all.GetValueOrDefault(title);
        }
    }

    public IReadOnlyList<BenchmarkRunInfo> GetRunning()
    {
        lock (_lock)
        {
            return _all.Values.ToList();
        }
    }

    public bool IsRunning(string title)
    {
        lock (_lock)
        {
            return _all.ContainsKey(title);
        }
    }

    private async Task ProcessQueue()
    {
        await foreach (var info in _queue.Reader.ReadAllAsync())
        {
            if (info.Cts.IsCancellationRequested)
            {
                Remove(info);
                continue;
            }

            await RunBenchmark(info);
        }
    }

    private async Task RunBenchmark(BenchmarkRunInfo info)
    {
        try
        {
            await info.Test.Start(info.Progress, info.Cts.Token).WaitAsync(info.Cts.Token);
        }
        catch (OperationCanceledException) when (info.Cts.IsCancellationRequested)
        {
            info.Progress.SetStatus(OperationStatus.Cancelled);
            _logger.LogInformation("[BenchmarkRunner] Benchmark {Title} cancelled", info.Test.Title);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[BenchmarkRunner] Benchmark {Title} failed", info.Test.Title);
        }
        finally
        {
            Remove(info);
            _completed.Invoke(info.Test.Title);
        }
    }

    private void Remove(BenchmarkRunInfo info)
    {
        lock (_lock)
        {
            if (_all.TryGetValue(info.Test.Title, out var current) && current == info)
                _all.Remove(info.Test.Title);
        }

        info.Cts.Dispose();
    }
}