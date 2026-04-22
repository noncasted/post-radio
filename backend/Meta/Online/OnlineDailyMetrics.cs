using System.Globalization;
using Cluster.Discovery;
using Common;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;

namespace Meta.Online;

public interface IOnlineDailyMetrics : IGrainWithStringKey
{
    Task Update(OnlineDailyData data);
    Task<OnlineDailyData> GetData();
}

[GenerateSerializer]
public class OnlineDailyData
{
    [Id(0)] public string Date { get; set; } = string.Empty;
    [Id(1)] public int PeakCount { get; set; }
    [Id(2)] public List<OnlineHistoryBucket> Hourly { get; set; } = [];
    [Id(3)] public DateTime UpdatedAtUtc { get; set; }
}

[GenerateSerializer]
[GrainState(Table = "online", State = "daily_metrics", Lookup = "OnlineDailyMetrics", Key = GrainKeyType.String)]
public class OnlineDailyState : IStateValue
{
    [Id(0)] public string Date { get; set; } = string.Empty;
    [Id(1)] public int PeakCount { get; set; }
    [Id(2)] public List<OnlineHistoryBucket> Hourly { get; set; } = [];
    [Id(3)] public DateTime UpdatedAtUtc { get; set; }

    public int Version => 0;
}

public interface IOnlineDailyCollection : IStateCollection<string, OnlineDailyState>
{
}

public class OnlineDailyCollection : StateCollection<string, OnlineDailyState>, IOnlineDailyCollection
{
    public OnlineDailyCollection(
        StateCollectionUtils<string, OnlineDailyState> utils,
        ILogger<StateCollection<string, OnlineDailyState>> logger)
        : base(utils, logger)
    {
    }
}

public class OnlineDailyMetrics : Grain, IOnlineDailyMetrics
{
    public OnlineDailyMetrics(
        [State] State<OnlineDailyState> state,
        IStateCollection<string, OnlineDailyState> collection)
    {
        _state = state;
        _collection = collection;
    }

    private readonly State<OnlineDailyState> _state;
    private readonly IStateCollection<string, OnlineDailyState> _collection;

    public async Task Update(OnlineDailyData data)
    {
        var updated = await _state.Update(state =>
        {
            state.Date = string.IsNullOrWhiteSpace(data.Date)
                ? this.GetPrimaryKeyString()
                : data.Date;

            state.PeakCount = Math.Max(state.PeakCount, data.PeakCount);
            state.UpdatedAtUtc = data.UpdatedAtUtc;
            state.Hourly = MergeHourly(state.Hourly, data.Hourly);
        });

        await _collection.OnUpdated(this.GetPrimaryKeyString(), updated);
    }

    public async Task<OnlineDailyData> GetData()
    {
        var state = await _state.ReadValue();

        return new OnlineDailyData
        {
            Date = string.IsNullOrWhiteSpace(state.Date) ? this.GetPrimaryKeyString() : state.Date,
            PeakCount = state.PeakCount,
            Hourly = state.Hourly,
            UpdatedAtUtc = state.UpdatedAtUtc
        };
    }

    private static List<OnlineHistoryBucket> MergeHourly(
        IReadOnlyList<OnlineHistoryBucket> existing,
        IReadOnlyList<OnlineHistoryBucket> incoming)
    {
        var buckets = existing.ToDictionary(bucket => bucket.BucketStartUtc, bucket => bucket.PeakCount);

        foreach (var bucket in incoming)
        {
            buckets[bucket.BucketStartUtc] = buckets.TryGetValue(bucket.BucketStartUtc, out var existingPeak)
                ? Math.Max(existingPeak, bucket.PeakCount)
                : bucket.PeakCount;
        }

        return buckets.OrderBy(kv => kv.Key)
                      .Select(kv => new OnlineHistoryBucket
                      {
                          BucketStartUtc = kv.Key,
                          PeakCount = kv.Value
                      })
                      .ToList();
    }
}

public class OnlineHistoryRecorder : ICoordinatorSetupCompleted
{
    public OnlineHistoryRecorder(
        IOnlineTracker onlineTracker,
        IOrleans orleans,
        IServiceEnvironment environment,
        ILogger<OnlineHistoryRecorder> logger)
    {
        _onlineTracker = onlineTracker;
        _orleans = orleans;
        _environment = environment;
        _logger = logger;
    }

    private readonly IOnlineTracker _onlineTracker;
    private readonly IOrleans _orleans;
    private readonly IServiceEnvironment _environment;
    private readonly ILogger<OnlineHistoryRecorder> _logger;

    public Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        if (_environment.Tag != ServiceTag.Meta)
            return Task.CompletedTask;

        Loop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    private async Task Loop(IReadOnlyLifetime lifetime)
    {
        while (!lifetime.IsTerminated)
        {
            try
            {
                await PersistToday();
                await Task.Delay(GetDelayToNextHour(DateTime.UtcNow), lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[OnlineHistory] Failed to persist online metrics");

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), lifetime.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private async Task PersistToday()
    {
        var snapshot = _onlineTracker.GetSnapshot();
        var now = DateTime.UtcNow;
        var key = ToDateKey(now);
        var hourly = snapshot.Hourly.Where(bucket => ToDateKey(bucket.BucketStartUtc) == key).ToList();

        var grain = _orleans.GetGrain<IOnlineDailyMetrics>(key);
        await grain.Update(new OnlineDailyData
        {
            Date = key,
            PeakCount = hourly.Count == 0 ? snapshot.Count : hourly.Max(bucket => bucket.PeakCount),
            Hourly = hourly,
            UpdatedAtUtc = now
        });
    }

    private static TimeSpan GetDelayToNextHour(DateTime now)
    {
        var nextHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc)
            .AddHours(1);

        return nextHour - now;
    }

    public static string ToDateKey(DateTime value)
    {
        return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
