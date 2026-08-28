using System.Globalization;
using Frontend.Shared;

namespace Frontend.Components;

internal static class AudioSkipDetailBuilder
{
    private const int MaxReportedEvents = 12;

    public static IReadOnlyList<KeyValuePair<string, string?>> Build(SongDto? song, TrackDiagnostics? diagnostics)
    {
        var list = new List<KeyValuePair<string, string?>>();
        void Add(string key, string? value) => list.Add(new KeyValuePair<string, string?>(key, value));

        if (song != null)
            Add("songId", song.Id.ToString(CultureInfo.InvariantCulture));

        if (diagnostics == null)
            return list;

        AddPlaybackDetails(Add, diagnostics);
        AddErrorDetails(Add, diagnostics);
        AddResumeDetails(Add, diagnostics);
        AddBrowserDetails(Add, diagnostics);
        AddEventTrail(Add, diagnostics);

        return list;
    }

    private static void AddPlaybackDetails(Action<string, string?> add, TrackDiagnostics diagnostics)
    {
        add("ct", AudioPlayerFormatters.FormatNumber(diagnostics.CurrentTime));
        add("dur", AudioPlayerFormatters.FormatNumber(diagnostics.Duration));
        add("rs", $"{diagnostics.ReadyState} ({AudioPlayerFormatters.ReadyStateName(diagnostics.ReadyState)})");
        add("ns", $"{diagnostics.NetworkState} ({AudioPlayerFormatters.NetworkStateName(diagnostics.NetworkState)})");
        add("paused", diagnostics.Paused.ToString());
        add("ended", diagnostics.Ended.ToString());

        if (diagnostics.BufferedEnd.HasValue)
            add("bufferedEnd", AudioPlayerFormatters.FormatNumber(diagnostics.BufferedEnd));

        add("startedAgoSec", Format(diagnostics.SinceStartSeconds));
        add("progressAgoSec", Format(diagnostics.SinceProgressSeconds));

        if (diagnostics.BufferingCount > 0)
        {
            add("bufferingCount", diagnostics.BufferingCount.ToString(CultureInfo.InvariantCulture));
            add("bufferingTotalSec", Format(diagnostics.BufferingTotalSeconds));
        }
    }

    private static void AddErrorDetails(Action<string, string?> add, TrackDiagnostics diagnostics)
    {
        if (diagnostics.ErrorCode.HasValue)
            add("errCode", $"{diagnostics.ErrorCode} ({AudioPlayerFormatters.MediaErrorName(diagnostics.ErrorCode.Value)})");

        if (!string.IsNullOrWhiteSpace(diagnostics.ErrorMessage))
            add("errMsg", diagnostics.ErrorMessage);

        if (!string.IsNullOrWhiteSpace(diagnostics.PlayErrorName))
            add("playError", diagnostics.PlayErrorName);

        if (!string.IsNullOrWhiteSpace(diagnostics.PlayErrorMessage))
            add("playErrorMsg", diagnostics.PlayErrorMessage);
    }

    private static void AddResumeDetails(Action<string, string?> add, TrackDiagnostics diagnostics)
    {
        var attempts = diagnostics.ResumeAttempts;

        if (attempts is not { Count: > 0 })
            return;

        add("resumeAttempts", attempts.Count.ToString(CultureInfo.InvariantCulture));

        foreach (var attempt in attempts)
        {
            var value = $"resumed={attempt.Resumed} detail={attempt.Detail ?? "-"} nudged={attempt.Nudged} " +
                        $"before={AudioPlayerFormatters.FormatNumber(attempt.BeforeTime)} " +
                        $"after={AudioPlayerFormatters.FormatNumber(attempt.AfterTime)} " +
                        $"elapsedMs={attempt.ElapsedMs}";

            if (!string.IsNullOrWhiteSpace(attempt.ErrorMessage))
                value += $" error={attempt.ErrorMessage}";

            add($"resume{attempt.Attempt}", value);
        }
    }

    private static void AddBrowserDetails(Action<string, string?> add, TrackDiagnostics diagnostics)
    {
        add("domHidden", diagnostics.Hidden.ToString());

        if (!string.IsNullOrWhiteSpace(diagnostics.VisibilityState))
            add("visibilityState", diagnostics.VisibilityState);

        if (diagnostics.HiddenSeconds > 0)
            add("hiddenSec", Format(diagnostics.HiddenSeconds));

        if (!string.IsNullOrWhiteSpace(diagnostics.UserAgent))
            add("userAgent", diagnostics.UserAgent);
    }

    private static void AddEventTrail(Action<string, string?> add, TrackDiagnostics diagnostics)
    {
        var events = diagnostics.Events;

        if (events is not { Count: > 0 })
            return;

        var tail = events.Count > MaxReportedEvents
            ? events.Skip(events.Count - MaxReportedEvents)
            : events;

        add("events", string.Join(" ", tail.Select(FormatEvent)));
    }

    private static string FormatEvent(AudioEventInfo info)
    {
        return $"{info.Name}@{Format(info.AtSeconds)}/{AudioPlayerFormatters.FormatNumber(info.CurrentTime)}";
    }

    private static string Format(double value) => value.ToString("F1", CultureInfo.InvariantCulture);
}
