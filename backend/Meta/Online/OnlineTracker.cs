using Cluster.Deploy;
using Cluster.Discovery;
using Common;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Meta.Online;

public interface IOnlineTracker
{
    void Touch(string? sessionId);
    OnlineLiveData GetSnapshot();
}

[GenerateSerializer]
public class OnlineLiveData
{
    [Id(0)] public int Count { get; set; }
    [Id(1)] public DateTime UpdatedAtUtc { get; set; }
    [Id(2)] public List<OnlineSessionEntry> Sessions { get; set; } = [];
    [Id(3)] public List<OnlineHistoryBucket> Hourly { get; set; } = [];
}

[GenerateSerializer]
public class OnlineSessionEntry
{
    [Id(0)] public string SessionId { get; set; } = string.Empty;
    [Id(1)] public DateTime StartedAtUtc { get; set; }
    [Id(2)] public DateTime LastSeenAtUtc { get; set; }
}

[GenerateSerializer]
public class OnlineHistoryBucket
{
    [Id(0)] public DateTime BucketStartUtc { get; set; }
    [Id(1)] public int PeakCount { get; set; }
}

public class OnlineTracker : IOnlineTracker, ICoordinatorSetupCompleted
{
    public OnlineTracker(
        ILiveState<OnlineLiveData> liveState,
        IServiceEnvironment environment,
        ILogger<OnlineTracker> logger)
    {
        _liveState = liveState;
        _environment = environment;
        _logger = logger;
    }

    private static readonly TimeSpan OnlineTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PublishInterval = TimeSpan.FromSeconds(15);
    private const int MaxSessionIdLength = 128;
    private const int MaxHourlyBuckets = 24;

    private readonly ILiveState<OnlineLiveData> _liveState;
    private readonly IServiceEnvironment _environment;
    private readonly ILogger<OnlineTracker> _logger;
    private readonly object _sync = new();
    private readonly Dictionary<string, OnlineSessionEntry> _sessions = new(StringComparer.Ordinal);
    private readonly SortedDictionary<DateTime, OnlineHistoryBucket> _hourly = new();

    public Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        if (_environment.Tag != ServiceTag.Meta)
            return Task.CompletedTask;

        Loop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    public void Touch(string? sessionId)
    {
        sessionId = NormalizeSessionId(sessionId);
        if (sessionId == null)
            return;

        var now = DateTime.UtcNow;

        lock (_sync)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                session.LastSeenAtUtc = now;
                return;
            }

            _sessions[sessionId] = new OnlineSessionEntry
            {
                SessionId = sessionId,
                StartedAtUtc = now,
                LastSeenAtUtc = now
            };
        }
    }

    public OnlineLiveData GetSnapshot()
    {
        var now = DateTime.UtcNow;

        lock (_sync)
        {
            Cleanup(now);

            var count = _sessions.Count;
            AddSample(_hourly, ToHourBucket(now), count, MaxHourlyBuckets);

            return CreateSnapshot(now);
        }
    }

    private async Task Loop(IReadOnlyLifetime lifetime)
    {
        while (!lifetime.IsTerminated)
        {
            try
            {
                await PublishSnapshot();
                await Task.Delay(PublishInterval, lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[OnlineTracker] Failed to publish online state");

                try
                {
                    await Task.Delay(PublishInterval, lifetime.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private async Task PublishSnapshot()
    {
        await _liveState.SetValue(GetSnapshot());
    }

    private void Cleanup(DateTime now)
    {
        var cutoff = now - OnlineTtl;
        var expired = _sessions
            .Where(kv => kv.Value.LastSeenAtUtc < cutoff)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in expired)
            _sessions.Remove(key);
    }

    private static void AddSample(
        SortedDictionary<DateTime, OnlineHistoryBucket> buckets,
        DateTime bucketStart,
        int count,
        int maxBuckets)
    {
        if (!buckets.TryGetValue(bucketStart, out var bucket))
        {
            bucket = new OnlineHistoryBucket { BucketStartUtc = bucketStart };
            buckets.Add(bucketStart, bucket);
        }

        bucket.PeakCount = Math.Max(bucket.PeakCount, count);

        while (buckets.Count > maxBuckets)
            buckets.Remove(buckets.Keys.First());
    }

    private static DateTime ToHourBucket(DateTime value)
    {
        return new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0, DateTimeKind.Utc);
    }

    private static string? NormalizeSessionId(string? value)
    {
        value = value?.Trim();

        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Length <= MaxSessionIdLength
            ? value
            : value[..MaxSessionIdLength];
    }

    private OnlineLiveData CreateSnapshot(DateTime now)
    {
        return new OnlineLiveData
        {
            Count = _sessions.Count,
            UpdatedAtUtc = now,
            Sessions = _sessions.Values
                                .OrderByDescending(session => session.LastSeenAtUtc)
                                .ToList(),
            Hourly = _hourly.Values.ToList()
        };
    }
}

public static class OnlineServicesExtensions
{
    public static IHostApplicationBuilder AddOnlineServices(this IHostApplicationBuilder builder)
    {
        builder.AddLiveState<OnlineLiveData>();

        builder.AddStateCollection<OnlineDailyCollection, string, OnlineDailyState>()
               .As<IOnlineDailyCollection>();

        builder.Add<OnlineTracker>()
               .As<IOnlineTracker>()
               .As<ICoordinatorSetupCompleted>();

        builder.Add<OnlineHistoryRecorder>()
               .As<ICoordinatorSetupCompleted>();

        return builder;
    }
}
