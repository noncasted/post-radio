using System.Globalization;
using Frontend.Shared;

namespace Frontend.Client.Components;

internal static class AudioSkipDetailBuilder
{
    public static IReadOnlyList<KeyValuePair<string, string?>> Build(AudioSkipDetailContext context)
    {
        var list = new List<KeyValuePair<string, string?>>();
        void Add(string key, string? value) => list.Add(new KeyValuePair<string, string?>(key, value));

        Add("gen", context.Generation.ToString(CultureInfo.InvariantCulture));
        if (context.Song != null)
            Add("songId", context.Song.Id.ToString(CultureInfo.InvariantCulture));

        if (context.Snapshot != null)
            AddSnapshotDetails(Add, context.Snapshot);
        else
            AddCachedDetails(Add, context);

        AddRuntimeDetails(Add, context);
        AddBrowserDetails(Add, context.Snapshot);

        return list;
    }

    private static void AddSnapshotDetails(Action<string, string?> add, AudioStateSnapshot snapshot)
    {
        add("ct", AudioPlayerFormatters.FormatNumber(snapshot.CurrentTime));
        add("dur", AudioPlayerFormatters.FormatNumber(snapshot.Duration));
        add("rs", $"{snapshot.ReadyState} ({AudioPlayerFormatters.ReadyStateName(snapshot.ReadyState)})");
        add("ns", $"{snapshot.NetworkState} ({AudioPlayerFormatters.NetworkStateName(snapshot.NetworkState)})");
        add("paused", snapshot.Paused.ToString());
        add("ended", snapshot.Ended.ToString());

        if (snapshot.ErrorCode.HasValue)
            add("errCode", $"{snapshot.ErrorCode} ({AudioPlayerFormatters.MediaErrorName(snapshot.ErrorCode.Value)})");
        if (!string.IsNullOrWhiteSpace(snapshot.ErrorMessage))
            add("errMsg", snapshot.ErrorMessage);
        if (snapshot.Buffered is { Count: > 0 })
            add("bufferedEnd", AudioPlayerFormatters.FormatNumber(snapshot.Buffered[^1].End));
    }

    private static void AddCachedDetails(Action<string, string?> add, AudioSkipDetailContext context)
    {
        if (context.LastCurrentTime.HasValue)
            add("ct", AudioPlayerFormatters.FormatNumber(context.LastCurrentTime));
        if (context.LastDuration.HasValue)
            add("dur", AudioPlayerFormatters.FormatNumber(context.LastDuration));
        if (context.LastReadyState.HasValue)
            add("rs", $"{context.LastReadyState} ({AudioPlayerFormatters.ReadyStateName(context.LastReadyState.Value)})");
        if (context.LastNetworkState.HasValue)
            add("ns", $"{context.LastNetworkState} ({AudioPlayerFormatters.NetworkStateName(context.LastNetworkState.Value)})");
    }

    private static void AddRuntimeDetails(Action<string, string?> add, AudioSkipDetailContext context)
    {
        add("buffering", context.IsBuffering.ToString());
        if (context.BufferingStartedUtc.HasValue)
            add("bufferingAgoSec", AudioPlayerTiming.GetSecondsSince(context.BufferingStartedUtc).ToString("F1", CultureInfo.InvariantCulture));
        if (context.LastProgressUtc != DateTime.MinValue)
            add("progressAgoSec", AudioPlayerTiming.GetSecondsSince(context.LastProgressUtc).ToString("F1", CultureInfo.InvariantCulture));
        if (context.LoadAndPlayCompletedUtc.HasValue)
            add("startedAgoSec", AudioPlayerTiming.GetSecondsSince(context.LoadAndPlayCompletedUtc).ToString("F1", CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(context.LastLoadAndPlayError))
            add("loadAndPlayError", context.LastLoadAndPlayError);
        if (context.LastAudioErrorCode.HasValue)
            add("lastAudioErrCode", $"{context.LastAudioErrorCode} ({AudioPlayerFormatters.MediaErrorName(context.LastAudioErrorCode.Value)})");
        if (!string.IsNullOrWhiteSpace(context.LastAudioErrorMessage))
            add("lastAudioErrMsg", context.LastAudioErrorMessage);

        add("hidden", context.IsHidden.ToString());
        if (context.HiddenSinceUtc.HasValue)
            add("hiddenAgoSec", AudioPlayerTiming.GetSecondsSince(context.HiddenSinceUtc).ToString("F1", CultureInfo.InvariantCulture));
        if (context.ProgressTimeoutResumeAttempts > 0)
            add("resumeAttempts", context.ProgressTimeoutResumeAttempts.ToString(CultureInfo.InvariantCulture));
        if (context.LastResumeAttemptUtc.HasValue)
            add("resumeAgoSec", AudioPlayerTiming.GetSecondsSince(context.LastResumeAttemptUtc).ToString("F1", CultureInfo.InvariantCulture));
    }

    private static void AddBrowserDetails(Action<string, string?> add, AudioStateSnapshot? snapshot)
    {
        if (snapshot == null)
            return;

        if (snapshot.Hidden.HasValue)
            add("domHidden", snapshot.Hidden.Value.ToString());
        if (!string.IsNullOrWhiteSpace(snapshot.VisibilityState))
            add("visibilityState", snapshot.VisibilityState);
        if (!string.IsNullOrWhiteSpace(snapshot.UserAgent))
            add("userAgent", snapshot.UserAgent);
    }
}

internal sealed record AudioSkipDetailContext(
    int Generation,
    SongDto? Song,
    AudioStateSnapshot? Snapshot,
    double? LastCurrentTime,
    double? LastDuration,
    int? LastReadyState,
    int? LastNetworkState,
    bool IsBuffering,
    DateTime? BufferingStartedUtc,
    DateTime LastProgressUtc,
    DateTime? LoadAndPlayCompletedUtc,
    string? LastLoadAndPlayError,
    int? LastAudioErrorCode,
    string? LastAudioErrorMessage,
    bool IsHidden,
    DateTime? HiddenSinceUtc,
    int ProgressTimeoutResumeAttempts,
    DateTime? LastResumeAttemptUtc);