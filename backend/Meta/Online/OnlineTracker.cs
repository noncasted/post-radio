using System.Security.Cryptography;
using System.Text;
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
    /// <summary>
    /// Registers one listening client. Called only while audio is actually playing in the
    /// browser - opening the page is not presence.
    /// </summary>
    void Touch(string? clientId);

    /// <summary>Seeds the hourly history after a restart, so the chart survives a redeploy.</summary>
    void Restore(IReadOnlyList<OnlineHistoryBucket> hourly);

    OnlineLiveData GetSnapshot();
}

[GenerateSerializer]
public class OnlineLiveData
{
    [Id(0)] public int Count { get; set; }
    [Id(1)] public DateTime UpdatedAtUtc { get; set; }
    [Id(2)] public List<OnlineListenerEntry> Listeners { get; set; } = [];
    [Id(3)] public List<OnlineHistoryBucket> Hourly { get; set; } = [];
}

[GenerateSerializer]
public class OnlineListenerEntry
{
    [Id(0)] public string ClientId { get; set; } = string.Empty;
    [Id(1)] public DateTime StartedAtUtc { get; set; }
    [Id(2)] public DateTime LastSeenAtUtc { get; set; }
}

[GenerateSerializer]
public class OnlineHistoryBucket
{
    [Id(0)] public DateTime BucketStartUtc { get; set; }
    [Id(1)] public int PeakCount { get; set; }
}

/// <summary>
/// Counts distinct listening devices. A device is identified by the fingerprint the frontend
/// sends with every heartbeat, so several tabs of one browser collapse into one listener.
/// </summary>
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

    /// <summary>
    /// Three heartbeat intervals: background tabs get their timers throttled to roughly one
    /// tick per minute, and a listener must not blink out because of that.
    /// </summary>
    public static readonly TimeSpan ListenerTtl = TimeSpan.FromMinutes(3);

    private static readonly TimeSpan PublishInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan HistoryWindow = TimeSpan.FromHours(24);
    private const int MaxClientIdLength = 128;
    private const int MaxHourlyBuckets = 24;

    private readonly ILiveState<OnlineLiveData> _liveState;
    private readonly IServiceEnvironment _environment;
    private readonly ILogger<OnlineTracker> _logger;
    private readonly object _sync = new();
    private readonly Dictionary<string, OnlineListenerEntry> _listeners = new(StringComparer.Ordinal);
    private readonly SortedDictionary<DateTime, OnlineHistoryBucket> _hourly = new();

    public Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        if (_environment.Tag != ServiceTag.Meta)
            return Task.CompletedTask;

        Loop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    public void Touch(string? clientId)
    {
        var key = NormalizeClientId(clientId);

        if (key == null)
            return;

        var now = DateTime.UtcNow;

        lock (_sync)
        {
            if (_listeners.TryGetValue(key, out var listener))
            {
                listener.LastSeenAtUtc = now;
                return;
            }

            _listeners[key] = new OnlineListenerEntry
            {
                ClientId = key,
                StartedAtUtc = now,
                LastSeenAtUtc = now
            };
        }
    }

    public void Restore(IReadOnlyList<OnlineHistoryBucket> hourly)
    {
        var cutoff = ToHourBucket(DateTime.UtcNow - HistoryWindow);

        lock (_sync)
        {
            foreach (var bucket in hourly)
            {
                if (bucket.BucketStartUtc < cutoff)
                    continue;

                AddSample(_hourly, bucket.BucketStartUtc, bucket.PeakCount, MaxHourlyBuckets);
            }
        }

        _logger.LogInformation("[OnlineTracker] Restored {Count} hourly buckets", hourly.Count);
    }

    public OnlineLiveData GetSnapshot()
    {
        var now = DateTime.UtcNow;

        lock (_sync)
        {
            Cleanup(now);

            var count = _listeners.Count;
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
        var cutoff = now - ListenerTtl;
        var expired = _listeners
                      .Where(kv => kv.Value.LastSeenAtUtc < cutoff)
                      .Select(kv => kv.Key)
                      .ToList();

        foreach (var key in expired)
            _listeners.Remove(key);

        var historyCutoff = ToHourBucket(now - HistoryWindow);
        var staleBuckets = _hourly.Keys.Where(bucketStart => bucketStart < historyCutoff).ToList();

        foreach (var bucketStart in staleBuckets)
            _hourly.Remove(bucketStart);
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

    /// <summary>
    /// The raw value is a browser fingerprint, so it is hashed before it is stored or shown:
    /// the tracker only ever needs to tell two devices apart, never to identify one.
    /// </summary>
    private static string? NormalizeClientId(string? value)
    {
        value = value?.Trim();

        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxClientIdLength)
            return null;

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(hash.AsSpan(0, 8));
    }

    private OnlineLiveData CreateSnapshot(DateTime now)
    {
        return new OnlineLiveData
        {
            Count = _listeners.Count,
            UpdatedAtUtc = now,
            Listeners = _listeners.Values
                                  .OrderByDescending(listener => listener.LastSeenAtUtc)
                                  .ToList(),
            Hourly = _hourly.Values
                            .Select(bucket => new OnlineHistoryBucket
                            {
                                BucketStartUtc = bucket.BucketStartUtc,
                                PeakCount = bucket.PeakCount
                            })
                            .ToList()
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
